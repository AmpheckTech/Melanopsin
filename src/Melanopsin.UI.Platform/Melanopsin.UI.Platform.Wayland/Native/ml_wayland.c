#define _GNU_SOURCE

#include "ml_wayland.h"

#include <errno.h>
#include <fcntl.h>
#include <linux/input-event-codes.h>
#include <math.h>
#include <poll.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <sys/syscall.h>
#include <unistd.h>
#include <libdecor.h>
#include <wayland-client.h>
#include <wayland-cursor.h>
#include <xkbcommon/xkbcommon.h>

#include "Protocol/fractional-scale-v1-client-protocol.h"
#include "Protocol/viewporter-client-protocol.h"

#define ML_BUFFER_COUNT 3
#define ML_BUFFER_CAPACITY_STEP 256
#define ML_ERROR_CAPACITY 512
#define ML_CURSOR_SIZE 24

static char ml_global_error[ML_ERROR_CAPACITY];

struct ml_host;
struct ml_window;
struct ml_output;
struct ml_buffer_group;

typedef struct ml_buffer_slot {
    struct wl_buffer *buffer;
    struct ml_buffer_group *group;
    void *data;
    bool busy;
    struct wl_buffer_listener listener;
} ml_buffer_slot;

typedef struct ml_buffer_group {
    struct ml_window *window;
    struct ml_buffer_group *next;
    void *mapping;
    size_t mapping_size;
    int width;
    int height;
    int stride;
    size_t slot_size;
    int live_buffers;
    bool retiring;
    ml_buffer_slot slots[ML_BUFFER_COUNT];
} ml_buffer_group;

typedef struct ml_output {
    struct ml_host *host;
    struct wl_output *output;
    int32_t scale;
    struct ml_output *next;
    struct wl_output_listener listener;
} ml_output;

typedef struct ml_window {
    struct ml_host *host;
    struct ml_window *next;
    uint64_t window_id;
    struct wl_surface *surface;
    struct libdecor_frame *frame;
    struct wp_fractional_scale_v1 *fractional_scale;
    struct wp_viewport *viewport;
    struct wl_callback *frame_callback;
    ml_buffer_group *buffers;
    struct ml_output *entered_outputs[8];
    size_t entered_output_count;
    int32_t logical_width;
    int32_t logical_height;
    double scale;
    bool configured;
    bool closed;
    bool has_fractional_scale;
    double pointer_x;
    double pointer_y;
    struct wl_surface_listener surface_listener;
    struct wp_fractional_scale_v1_listener fractional_scale_listener;
} ml_window;

typedef struct ml_host {
    struct wl_display *display;
    struct wl_registry *registry;
    struct wl_compositor *compositor;
    struct wl_shm *shm;
    struct wl_seat *seat;
    struct wl_pointer *pointer;
    struct wl_keyboard *keyboard;
    struct libdecor *decor;
    struct wp_fractional_scale_manager_v1 *fractional_scale_manager;
    struct wp_viewporter *viewporter;
    struct wl_cursor_theme *cursor_theme;
    struct wl_surface *cursor_surface;
    struct xkb_context *xkb_context;
    struct xkb_keymap *xkb_keymap;
    struct xkb_state *xkb_state;
    ml_output *outputs;
    ml_window *windows;
    ml_window *pointer_window;
    ml_window *keyboard_window;
    ml_window *axis_window;
    ml_wayland_callbacks callbacks;
    void *callback_context;
    bool has_fractional_scale;
    bool has_viewporter;
    bool owns_display;
    uint32_t pointer_enter_serial;
    uint32_t pointer_time;
    uint32_t keyboard_modifiers;
    double axis_horizontal;
    double axis_vertical;
    int32_t axis_horizontal_value120;
    int32_t axis_vertical_value120;
    bool axis_pending;
    int wakeup_read_fd;
    int wakeup_write_fd;
    char error[ML_ERROR_CAPACITY];
    struct wl_registry_listener registry_listener;
    struct wl_seat_listener seat_listener;
    struct wl_pointer_listener pointer_listener;
    struct wl_keyboard_listener keyboard_listener;
} ml_host;

static void set_error(ml_host *host, const char *message) {
    if (host == NULL || message == NULL) {
        return;
    }

    snprintf(host->error, sizeof(host->error), "%s", message);
    snprintf(ml_global_error, sizeof(ml_global_error), "%s", message);
}

static void set_errno_error(ml_host *host, const char *operation) {
    if (host == NULL) {
        return;
    }

    snprintf(host->error, sizeof(host->error), "%s: %s", operation, strerror(errno));
    snprintf(ml_global_error, sizeof(ml_global_error), "%s", host->error);
}

static void notify_error(ml_host *host, const char *message) {
    set_error(host, message);
    if (host->callbacks.error != NULL) {
        host->callbacks.error(host->callback_context, message);
    }
}

static void wakeup_host(ml_host *host) {
    if (host == NULL || host->wakeup_write_fd < 0) {
        return;
    }

    const uint8_t token = 1;
    ssize_t result;
    do {
        result = write(host->wakeup_write_fd, &token, sizeof(token));
    } while (result < 0 && errno == EINTR);

    // The pipe is nonblocking: a full pipe already guarantees that the
    // consumer will observe the wakeup, so EAGAIN is not a failure.
}

static void clear_wakeup(ml_host *host) {
    if (host == NULL || host->wakeup_read_fd < 0) {
        return;
    }

    uint8_t buffer[64];
    ssize_t result;
    do {
        result = read(host->wakeup_read_fd, buffer, sizeof(buffer));
    } while (result > 0 || (result < 0 && errno == EINTR));
}

static double window_scale(const ml_window *window) {
    if (window == NULL || !isfinite(window->scale) || window->scale <= 0) {
        return 1.0;
    }

    return window->scale;
}

static int32_t scaled_dimension(int32_t logical, double scale) {
    if (logical <= 0) {
        return 1;
    }

    long value = lround((double)logical * scale);
    if (value < 1) {
        return 1;
    }

    if (value > INT32_MAX) {
        return INT32_MAX;
    }

    return (int32_t)value;
}

static void notify_configure(ml_window *window) {
    if (window == NULL || !window->configured) {
        return;
    }

    ml_host *host = window->host;
    if (host->callbacks.configure != NULL) {
        host->callbacks.configure(
            host->callback_context,
            (uintptr_t)window,
            window->logical_width,
            window->logical_height,
            window_scale(window));
    }

    wakeup_host(host);
}

static void set_window_scale(ml_window *window, double scale) {
    if (window == NULL || !isfinite(scale) || scale <= 0) {
        return;
    }

    if (fabs(window->scale - scale) < 0.0001) {
        return;
    }

    window->scale = scale;
    notify_configure(window);
}

static ml_output *find_output(ml_host *host, struct wl_output *output) {
    for (ml_output *candidate = host->outputs; candidate != NULL; candidate = candidate->next) {
        if (candidate->output == output) {
            return candidate;
        }
    }

    return NULL;
}

static void update_fallback_scale(ml_window *window) {
    if (window == NULL || window->has_fractional_scale) {
        return;
    }

    int32_t scale = 1;
    for (size_t index = 0; index < window->entered_output_count; index++) {
        if (window->entered_outputs[index] != NULL &&
            window->entered_outputs[index]->scale > scale) {
            scale = window->entered_outputs[index]->scale;
        }
    }

    set_window_scale(window, (double)scale);
}

static void output_geometry(
    void *data,
    struct wl_output *output,
    int32_t x,
    int32_t y,
    int32_t physical_width,
    int32_t physical_height,
    int32_t subpixel,
    const char *make,
    const char *model,
    int32_t transform) {
    (void)data;
    (void)output;
    (void)x;
    (void)y;
    (void)physical_width;
    (void)physical_height;
    (void)subpixel;
    (void)make;
    (void)model;
    (void)transform;
}

static void output_mode(
    void *data,
    struct wl_output *output,
    uint32_t flags,
    int32_t width,
    int32_t height,
    int32_t refresh) {
    (void)data;
    (void)output;
    (void)flags;
    (void)width;
    (void)height;
    (void)refresh;
}

static void output_done(void *data, struct wl_output *output) {
    (void)data;
    (void)output;
}

static void output_scale(void *data, struct wl_output *output, int32_t factor) {
    ml_output *entry = data;
    (void)output;
    if (entry == NULL || factor <= 0) {
        return;
    }

    entry->scale = factor;
    for (ml_window *window = entry->host->windows; window != NULL; window = window->next) {
        for (size_t index = 0; index < window->entered_output_count; index++) {
            if (window->entered_outputs[index] == entry) {
                update_fallback_scale(window);
                break;
            }
        }
    }
}

static void output_name(void *data, struct wl_output *output, const char *name) {
    (void)data;
    (void)output;
    (void)name;
}

static void output_description(void *data, struct wl_output *output, const char *description) {
    (void)data;
    (void)output;
    (void)description;
}

static const struct wl_output_listener output_listener = {
    .geometry = output_geometry,
    .mode = output_mode,
    .done = output_done,
    .scale = output_scale,
    .name = output_name,
    .description = output_description,
};

static void surface_enter(void *data, struct wl_surface *surface, struct wl_output *output) {
    ml_window *window = data;
    (void)surface;
    ml_output *entry = find_output(window->host, output);
    if (entry == NULL) {
        return;
    }

    for (size_t index = 0; index < window->entered_output_count; index++) {
        if (window->entered_outputs[index] == entry) {
            return;
        }
    }

    if (window->entered_output_count <
        sizeof(window->entered_outputs) / sizeof(window->entered_outputs[0])) {
        window->entered_outputs[window->entered_output_count++] = entry;
    }

    update_fallback_scale(window);
}

static void surface_leave(void *data, struct wl_surface *surface, struct wl_output *output) {
    ml_window *window = data;
    (void)surface;
    ml_output *entry = find_output(window->host, output);
    if (entry == NULL) {
        return;
    }

    for (size_t index = 0; index < window->entered_output_count; index++) {
        if (window->entered_outputs[index] == entry) {
            window->entered_outputs[index] =
                window->entered_outputs[--window->entered_output_count];
            break;
        }
    }

    update_fallback_scale(window);
}

static void surface_preferred_buffer_scale(
    void *data,
    struct wl_surface *surface,
    int32_t factor) {
    ml_window *window = data;
    (void)surface;
    if (factor <= 0 || window->has_fractional_scale) {
        return;
    }

    set_window_scale(window, (double)factor);
}

static void surface_preferred_buffer_transform(
    void *data,
    struct wl_surface *surface,
    uint32_t transform) {
    (void)data;
    (void)surface;
    (void)transform;
}

static void fractional_scale_preferred(
    void *data,
    struct wp_fractional_scale_v1 *fractional_scale,
    uint32_t scale) {
    ml_window *window = data;
    (void)fractional_scale;
    if (scale == 0) {
        return;
    }

    set_window_scale(window, (double)scale / 120.0);
}

static void frame_callback_done(
    void *data,
    struct wl_callback *callback,
    uint32_t callback_data) {
    (void)callback_data;
    ml_window *window = data;
    if (window != NULL && window->frame_callback == callback) {
        window->frame_callback = NULL;
    }

    if (callback != NULL) {
        wl_callback_destroy(callback);
    }

    if (window != NULL) {
        wakeup_host(window->host);
    }
}

static const struct wl_callback_listener frame_callback_listener = {
    .done = frame_callback_done,
};

static void decoration_error(
    struct libdecor *context,
    enum libdecor_error error,
    const char *message) {
    (void)context;
    (void)error;
    const char *detail = message != NULL ? message : "unknown libdecor failure";
    snprintf(ml_global_error, sizeof(ml_global_error), "libdecor: %s", detail);
}

static void frame_configure(
    struct libdecor_frame *frame,
    struct libdecor_configuration *configuration,
    void *data) {
    ml_window *window = data;
    int32_t width = window->logical_width;
    int32_t height = window->logical_height;
    if (!libdecor_configuration_get_content_size(
            configuration,
            frame,
            &width,
            &height) ||
        width <= 0 ||
        height <= 0) {
        width = window->logical_width;
        height = window->logical_height;
    }

    if (width <= 0) {
        width = 1;
    }
    if (height <= 0) {
        height = 1;
    }

    struct libdecor_state *state = libdecor_state_new(width, height);
    if (state == NULL) {
        notify_error(window->host, "Wayland window configuration allocation failed.");
        return;
    }

    window->logical_width = width;
    window->logical_height = height;
    libdecor_frame_commit(frame, state, configuration);
    libdecor_state_free(state);
    window->configured = true;
    notify_configure(window);
}

static void frame_close(struct libdecor_frame *frame, void *data) {
    (void)frame;
    ml_window *window = data;
    window->closed = true;
    ml_host *host = window->host;
    if (host->callbacks.close != NULL) {
        host->callbacks.close(host->callback_context, (uintptr_t)window);
    }
}

static void frame_commit(struct libdecor_frame *frame, void *data) {
    (void)frame;
    ml_window *window = data;
    if (window->surface != NULL) {
        wl_surface_commit(window->surface);
    }
}

static struct libdecor_interface decoration_interface = {
    .error = decoration_error,
};

static struct libdecor_frame_interface frame_interface = {
    .configure = frame_configure,
    .close = frame_close,
    .commit = frame_commit,
};

static const struct wl_surface_listener surface_listener = {
    .enter = surface_enter,
    .leave = surface_leave,
    .preferred_buffer_scale = surface_preferred_buffer_scale,
    .preferred_buffer_transform = surface_preferred_buffer_transform,
};

static const struct wp_fractional_scale_v1_listener fractional_scale_listener = {
    .preferred_scale = fractional_scale_preferred,
};

static void seat_capabilities(void *data, struct wl_seat *seat, uint32_t capabilities) {
    ml_host *host = data;
    if ((capabilities & WL_SEAT_CAPABILITY_POINTER) != 0 && host->pointer == NULL) {
        host->pointer = wl_seat_get_pointer(seat);
        wl_pointer_add_listener(host->pointer, &host->pointer_listener, host);
    } else if ((capabilities & WL_SEAT_CAPABILITY_POINTER) == 0 && host->pointer != NULL) {
        wl_pointer_release(host->pointer);
        host->pointer = NULL;
        host->pointer_window = NULL;
    }

    if ((capabilities & WL_SEAT_CAPABILITY_KEYBOARD) != 0 && host->keyboard == NULL) {
        host->keyboard = wl_seat_get_keyboard(seat);
        wl_keyboard_add_listener(host->keyboard, &host->keyboard_listener, host);
    } else if ((capabilities & WL_SEAT_CAPABILITY_KEYBOARD) == 0 &&
               host->keyboard != NULL) {
        wl_keyboard_release(host->keyboard);
        host->keyboard = NULL;
        host->keyboard_window = NULL;
    }
}

static void seat_name(void *data, struct wl_seat *seat, const char *name) {
    (void)data;
    (void)seat;
    (void)name;
}

static ml_window *window_from_surface(ml_host *host, struct wl_surface *surface) {
    for (ml_window *window = host->windows; window != NULL; window = window->next) {
        if (window->surface == surface) {
            return window;
        }
    }

    return NULL;
}

static void set_cursor(ml_host *host, uint32_t serial) {
    if (host->pointer == NULL || host->cursor_theme == NULL) {
        return;
    }

    struct wl_cursor *cursor = wl_cursor_theme_get_cursor(host->cursor_theme, "default");
    if (cursor == NULL) {
        cursor = wl_cursor_theme_get_cursor(host->cursor_theme, "left_ptr");
    }

    if (cursor == NULL || cursor->image_count == 0) {
        return;
    }

    struct wl_cursor_image *image = cursor->images[0];
    struct wl_buffer *buffer = wl_cursor_image_get_buffer(image);
    if (buffer == NULL) {
        return;
    }

    if (host->cursor_surface == NULL) {
        host->cursor_surface = wl_compositor_create_surface(host->compositor);
        if (host->cursor_surface == NULL) {
            return;
        }
    }

    wl_surface_attach(host->cursor_surface, buffer, 0, 0);
    wl_surface_damage_buffer(
        host->cursor_surface,
        0,
        0,
        (int32_t)image->width,
        (int32_t)image->height);
    wl_surface_commit(host->cursor_surface);
    wl_pointer_set_cursor(
        host->pointer,
        serial,
        host->cursor_surface,
        (int32_t)image->hotspot_x,
        (int32_t)image->hotspot_y);
}

static void pointer_enter(
    void *data,
    struct wl_pointer *pointer,
    uint32_t serial,
    struct wl_surface *surface,
    wl_fixed_t surface_x,
    wl_fixed_t surface_y) {
    (void)pointer;
    ml_host *host = data;
    ml_window *window = window_from_surface(host, surface);
    if (window == NULL) {
        return;
    }

    host->pointer_enter_serial = serial;
    host->pointer_window = window;
    window->pointer_x = wl_fixed_to_double(surface_x);
    window->pointer_y = wl_fixed_to_double(surface_y);
    set_cursor(host, serial);
    if (host->callbacks.pointer != NULL) {
        host->callbacks.pointer(
            host->callback_context,
            (uintptr_t)window,
            0,
            window->pointer_x,
            window->pointer_y,
            0,
            host->pointer_time);
    }
}

static void pointer_leave(
    void *data,
    struct wl_pointer *pointer,
    uint32_t serial,
    struct wl_surface *surface) {
    (void)pointer;
    (void)serial;
    ml_host *host = data;
    ml_window *window = window_from_surface(host, surface);
    if (window == NULL) {
        return;
    }

    if (host->callbacks.pointer != NULL) {
        host->callbacks.pointer(
            host->callback_context,
            (uintptr_t)window,
            1,
            window->pointer_x,
            window->pointer_y,
            0,
            host->pointer_time);
    }

    if (host->pointer_window == window) {
        host->pointer_window = NULL;
    }
}

static void pointer_motion(
    void *data,
    struct wl_pointer *pointer,
    uint32_t time,
    wl_fixed_t surface_x,
    wl_fixed_t surface_y) {
    (void)pointer;
    ml_host *host = data;
    host->pointer_time = time;
    ml_window *window = host->pointer_window;

    if (window == NULL) {
        return;
    }

    window->pointer_x = wl_fixed_to_double(surface_x);
    window->pointer_y = wl_fixed_to_double(surface_y);
    if (host->callbacks.pointer != NULL) {
        host->callbacks.pointer(
            host->callback_context,
            (uintptr_t)window,
            2,
            window->pointer_x,
            window->pointer_y,
            0,
            time);
    }
}

static void pointer_button(
    void *data,
    struct wl_pointer *pointer,
    uint32_t serial,
    uint32_t time,
    uint32_t button,
    uint32_t state) {
    (void)pointer;
    (void)serial;
    ml_host *host = data;
    ml_window *window = host->pointer_window;

    if (window == NULL || host->callbacks.pointer == NULL) {
        return;
    }

    host->pointer_time = time;
    uint32_t phase = state == WL_POINTER_BUTTON_STATE_PRESSED ? 3 : 4;
    host->callbacks.pointer(
        host->callback_context,
        (uintptr_t)window,
        phase,
        window->pointer_x,
        window->pointer_y,
        button,
        time);
}

static void pointer_axis(
    void *data,
    struct wl_pointer *pointer,
    uint32_t time,
    uint32_t axis,
    wl_fixed_t value) {
    (void)pointer;
    ml_host *host = data;
    host->pointer_time = time;
    if (host->callbacks.axis == NULL) {
        return;
    }

    ml_window *window = host->pointer_window;

    if (window == NULL) {
        return;
    }

    double amount = wl_fixed_to_double(value);
    if (axis == WL_POINTER_AXIS_HORIZONTAL_SCROLL) {
        host->axis_horizontal += amount;
    } else if (axis == WL_POINTER_AXIS_VERTICAL_SCROLL) {
        host->axis_vertical += amount;
    }

    host->axis_window = window;
    host->axis_pending = true;
}

static void pointer_frame(void *data, struct wl_pointer *pointer) {
    (void)pointer;
    ml_host *host = data;
    if (!host->axis_pending || host->axis_window == NULL) {
        return;
    }

    if (host->callbacks.axis != NULL) {
        host->callbacks.axis(
            host->callback_context,
            (uintptr_t)host->axis_window,
            host->axis_horizontal,
            host->axis_vertical,
            host->axis_horizontal_value120,
            host->axis_vertical_value120,
            host->pointer_time);
    }

    host->axis_window = NULL;
    host->axis_horizontal = 0;
    host->axis_vertical = 0;
    host->axis_horizontal_value120 = 0;
    host->axis_vertical_value120 = 0;
    host->axis_pending = false;
}

static void pointer_axis_source(void *data, struct wl_pointer *pointer, uint32_t axis_source) {
    (void)data;
    (void)pointer;
    (void)axis_source;
}

static void pointer_axis_stop(
    void *data,
    struct wl_pointer *pointer,
    uint32_t time,
    uint32_t axis) {
    (void)data;
    (void)pointer;
    (void)time;
    (void)axis;
}

static void pointer_axis_discrete(
    void *data,
    struct wl_pointer *pointer,
    uint32_t axis,
    int32_t discrete) {
    (void)data;
    (void)pointer;
    (void)axis;
    (void)discrete;
}

static void pointer_axis_value120(
    void *data,
    struct wl_pointer *pointer,
    uint32_t axis,
    int32_t value120) {
    ml_host *host = data;
    (void)pointer;
    if (host->pointer_window == NULL) {
        return;
    }

    if (axis == WL_POINTER_AXIS_HORIZONTAL_SCROLL) {
        host->axis_horizontal_value120 += value120;
    } else if (axis == WL_POINTER_AXIS_VERTICAL_SCROLL) {
        host->axis_vertical_value120 += value120;
    }

    host->axis_window = host->pointer_window;
    host->axis_pending = true;
}

static void pointer_axis_relative_direction(
    void *data,
    struct wl_pointer *pointer,
    uint32_t axis,
    uint32_t direction) {
    (void)data;
    (void)pointer;
    (void)axis;
    (void)direction;
}

static void keyboard_keymap(
    void *data,
    struct wl_keyboard *keyboard,
    uint32_t format,
    int32_t fd,
    uint32_t size) {
    (void)keyboard;
    ml_host *host = data;
    if (format != WL_KEYBOARD_KEYMAP_FORMAT_XKB_V1) {
        close(fd);
        return;
    }

    char *mapping = mmap(NULL, size, PROT_READ, MAP_PRIVATE, fd, 0);
    close(fd);
    if (mapping == MAP_FAILED) {
        notify_error(host, "Wayland keyboard keymap could not be mapped.");
        return;
    }

    struct xkb_keymap *keymap = xkb_keymap_new_from_string(
        host->xkb_context,
        mapping,
        XKB_KEYMAP_FORMAT_TEXT_V1,
        XKB_KEYMAP_COMPILE_NO_FLAGS);
    munmap(mapping, size);
    if (keymap == NULL) {
        notify_error(host, "Wayland keyboard keymap is invalid.");
        return;
    }

    struct xkb_state *state = xkb_state_new(keymap);
    if (state == NULL) {
        xkb_keymap_unref(keymap);
        notify_error(host, "Wayland keyboard state could not be created.");
        return;
    }

    if (host->xkb_state != NULL) {
        xkb_state_unref(host->xkb_state);
    }

    if (host->xkb_keymap != NULL) {
        xkb_keymap_unref(host->xkb_keymap);
    }

    host->xkb_keymap = keymap;
    host->xkb_state = state;
}

static void keyboard_enter(
    void *data,
    struct wl_keyboard *keyboard,
    uint32_t serial,
    struct wl_surface *surface,
    struct wl_array *keys) {
    (void)keyboard;
    (void)serial;
    (void)keys;
    ml_host *host = data;
    ml_window *window = window_from_surface(host, surface);
    host->keyboard_window = window;
    if (window != NULL && host->callbacks.focus != NULL) {
        host->callbacks.focus(host->callback_context, (uintptr_t)window, 1);
    }
}

static void keyboard_leave(
    void *data,
    struct wl_keyboard *keyboard,
    uint32_t serial,
    struct wl_surface *surface) {
    (void)keyboard;
    (void)serial;
    ml_host *host = data;
    ml_window *window = window_from_surface(host, surface);
    if (window != NULL && host->callbacks.focus != NULL) {
        host->callbacks.focus(host->callback_context, (uintptr_t)window, 0);
    }

    if (host->keyboard_window == window) {
        host->keyboard_window = NULL;
    }
}

static uint32_t effective_modifiers(ml_host *host) {
    if (host->xkb_state == NULL) {
        return host->keyboard_modifiers;
    }

    uint32_t modifiers = 0;
    if (xkb_state_mod_name_is_active(
            host->xkb_state,
            XKB_MOD_NAME_SHIFT,
            XKB_STATE_MODS_EFFECTIVE) > 0) {
        modifiers |= 1u;
    }

    if (xkb_state_mod_name_is_active(
            host->xkb_state,
            XKB_MOD_NAME_CTRL,
            XKB_STATE_MODS_EFFECTIVE) > 0) {
        modifiers |= 2u;
    }

    if (xkb_state_mod_name_is_active(
            host->xkb_state,
            XKB_MOD_NAME_ALT,
            XKB_STATE_MODS_EFFECTIVE) > 0) {
        modifiers |= 4u;
    }

    if (xkb_state_mod_name_is_active(
            host->xkb_state,
            XKB_MOD_NAME_LOGO,
            XKB_STATE_MODS_EFFECTIVE) > 0) {
        modifiers |= 8u;
    }

    return modifiers;
}

static void keyboard_key(
    void *data,
    struct wl_keyboard *keyboard,
    uint32_t serial,
    uint32_t time,
    uint32_t key,
    uint32_t state) {
    (void)keyboard;
    (void)serial;
    ml_host *host = data;
    if (host->xkb_state == NULL || host->callbacks.key == NULL) {
        return;
    }

    uint32_t xkb_keycode = key + 8;
    xkb_keysym_t keysym = xkb_state_key_get_one_sym(host->xkb_state, xkb_keycode);
    uint32_t modifiers = effective_modifiers(host);
    char text[64] = {0};
    if (state == WL_KEYBOARD_KEY_STATE_PRESSED) {
        xkb_state_key_get_utf8(host->xkb_state, xkb_keycode, text, sizeof(text));
    }

    ml_window *window = host->keyboard_window;

    if (window == NULL) {
        return;
    }

    host->callbacks.key(
        host->callback_context,
        (uintptr_t)window,
        keysym,
        state,
        modifiers,
        text[0] == '\0' ? NULL : text,
        time);
}

static void keyboard_modifiers(
    void *data,
    struct wl_keyboard *keyboard,
    uint32_t serial,
    uint32_t mods_depressed,
    uint32_t mods_latched,
    uint32_t mods_locked,
    uint32_t group) {
    (void)keyboard;
    (void)serial;
    ml_host *host = data;
    if (host->xkb_state == NULL) {
        return;
    }

    xkb_state_update_mask(
        host->xkb_state,
        mods_depressed,
        mods_latched,
        mods_locked,
        0,
        0,
        group);
    host->keyboard_modifiers = effective_modifiers(host);
}

static void keyboard_repeat_info(
    void *data,
    struct wl_keyboard *keyboard,
    int32_t rate,
    int32_t delay) {
    (void)data;
    (void)keyboard;
    (void)rate;
    (void)delay;
}

static void registry_global(
    void *data,
    struct wl_registry *registry,
    uint32_t name,
    const char *interface,
    uint32_t version) {
    ml_host *host = data;
    if (strcmp(interface, wl_compositor_interface.name) == 0) {
        uint32_t bind_version = version < 6 ? version : 6;
        host->compositor = wl_registry_bind(
            registry,
            name,
            &wl_compositor_interface,
            bind_version);
    } else if (strcmp(interface, wl_shm_interface.name) == 0) {
        host->shm = wl_registry_bind(registry, name, &wl_shm_interface, 1);
    } else if (strcmp(interface, wl_seat_interface.name) == 0 && host->seat == NULL) {
        uint32_t bind_version = version < 5 ? version : 5;
        host->seat = wl_registry_bind(registry, name, &wl_seat_interface, bind_version);
        wl_seat_add_listener(host->seat, &host->seat_listener, host);
    } else if (strcmp(interface, wp_fractional_scale_manager_v1_interface.name) == 0) {
        host->fractional_scale_manager = wl_registry_bind(
            registry,
            name,
            &wp_fractional_scale_manager_v1_interface,
            1);
    } else if (strcmp(interface, wp_viewporter_interface.name) == 0) {
        host->viewporter = wl_registry_bind(
            registry,
            name,
            &wp_viewporter_interface,
            1);
    } else if (strcmp(interface, wl_output_interface.name) == 0) {
        ml_output *output = calloc(1, sizeof(*output));
        if (output == NULL) {
            return;
        }

        output->host = host;
        output->scale = 1;
        output->listener = output_listener;
        uint32_t bind_version = version < 2 ? version : 2;
        output->output = wl_registry_bind(
            registry,
            name,
            &wl_output_interface,
            bind_version);
        wl_output_add_listener(output->output, &output->listener, output);
        output->next = host->outputs;
        host->outputs = output;
    }
}

static void registry_global_remove(
    void *data,
    struct wl_registry *registry,
    uint32_t name) {
    (void)data;
    (void)registry;
    (void)name;
}

static void destroy_buffer_group(ml_buffer_group *group) {
    if (group == NULL) {
        return;
    }

    for (int index = 0; index < ML_BUFFER_COUNT; index++) {
        ml_buffer_slot *slot = &group->slots[index];
        if (slot->buffer != NULL) {
            wl_buffer_destroy(slot->buffer);
            slot->buffer = NULL;
        }
        slot->busy = false;
    }

    if (group->mapping != NULL) {
        munmap(group->mapping, group->mapping_size);
        group->mapping = NULL;
    }

}

static void unlink_buffer_group(ml_buffer_group *group) {
    if (group == NULL || group->window == NULL) {
        free(group);
        return;
    }

    ml_buffer_group **link = &group->window->buffers;
    while (*link != NULL && *link != group) {
        link = &(*link)->next;
    }

    if (*link == group) {
        *link = group->next;
    }

    free(group);
}

static void maybe_free_buffer_group(ml_buffer_group *group) {
    if (group == NULL || !group->retiring || group->live_buffers > 0) {
        return;
    }

    if (group->mapping != NULL) {
        munmap(group->mapping, group->mapping_size);
        group->mapping = NULL;
    }

    unlink_buffer_group(group);
}

static void buffer_release(void *data, struct wl_buffer *buffer) {
    (void)buffer;
    ml_buffer_slot *slot = data;
    if (slot == NULL) {
        return;
    }

    slot->busy = false;
    ml_buffer_group *group = slot->group;
    if (group == NULL || !group->retiring) {
        return;
    }

    if (slot->buffer != NULL) {
        wl_buffer_destroy(slot->buffer);
        slot->buffer = NULL;
    }

    if (group->live_buffers > 0) {
        group->live_buffers--;
    }

    maybe_free_buffer_group(group);
}

static void retire_buffer_group(ml_buffer_group *group) {
    if (group == NULL || group->retiring) {
        return;
    }

    group->retiring = true;
    for (int index = 0; index < ML_BUFFER_COUNT; index++) {
        ml_buffer_slot *slot = &group->slots[index];
        if (slot->busy) {
            continue;
        }

        if (slot->buffer != NULL) {
            wl_buffer_destroy(slot->buffer);
            slot->buffer = NULL;
        }

        if (group->live_buffers > 0) {
            group->live_buffers--;
        }
    }

    maybe_free_buffer_group(group);
}

static int create_anonymous_file(size_t size) {
    int fd = -1;
#ifdef SYS_memfd_create
    fd = (int)syscall(SYS_memfd_create, "melanopsin-wayland", 0);
#endif
    if (fd < 0) {
        char name[64];
        snprintf(name, sizeof(name), "/melanopsin-wayland-%ld", (long)getpid());
        fd = shm_open(name, O_CREAT | O_EXCL | O_RDWR, 0600);
        if (fd >= 0) {
            shm_unlink(name);
        }
    }

    if (fd < 0) {
        return -1;
    }

    if (ftruncate(fd, (off_t)size) != 0) {
        close(fd);
        return -1;
    }

    return fd;
}

static ml_buffer_group *create_buffer_group(ml_window *window, int width, int height) {
    if (width <= 0 || height <= 0) {
        return NULL;
    }

    int stride = width > INT32_MAX / 4 ? -1 : width * 4;
    if (stride <= 0) {
        set_error(window->host, "Wayland buffer width overflowed.");
        return NULL;
    }

    size_t frame_size = (size_t)stride * (size_t)height;
    if (frame_size == 0) {
        set_error(window->host, "Wayland buffer size is invalid.");
        return NULL;
    }

    long page_value = sysconf(_SC_PAGESIZE);
    size_t page_size = page_value > 0 ? (size_t)page_value : 4096;
    size_t slot_size = (frame_size + page_size - 1) / page_size * page_size;
    if (slot_size > SIZE_MAX / ML_BUFFER_COUNT) {
        set_error(window->host, "Wayland buffer allocation overflowed.");
        return NULL;
    }

    size_t mapping_size = slot_size * ML_BUFFER_COUNT;
    int fd = create_anonymous_file(mapping_size);
    if (fd < 0) {
        set_errno_error(window->host, "Wayland shared memory allocation failed");
        return NULL;
    }

    void *mapping = mmap(NULL, mapping_size, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
    if (mapping == MAP_FAILED) {
        close(fd);
        set_errno_error(window->host, "Wayland shared memory mapping failed");
        return NULL;
    }

    struct wl_shm_pool *pool = wl_shm_create_pool(
        window->host->shm,
        fd,
        (int32_t)mapping_size);
    close(fd);
    if (pool == NULL) {
        munmap(mapping, mapping_size);
        set_error(window->host, "Wayland shared memory pool creation failed.");
        return NULL;
    }

    ml_buffer_group *group = calloc(1, sizeof(*group));
    if (group == NULL) {
        wl_shm_pool_destroy(pool);
        munmap(mapping, mapping_size);
        set_error(window->host, "Wayland buffer bookkeeping allocation failed.");
        return NULL;
    }

    group->window = window;
    group->mapping = mapping;
    group->mapping_size = mapping_size;
    group->width = width;
    group->height = height;
    group->stride = stride;
    group->slot_size = slot_size;
    group->next = window->buffers;
    window->buffers = group;

    for (int index = 0; index < ML_BUFFER_COUNT; index++) {
        ml_buffer_slot *slot = &group->slots[index];
        slot->group = group;
        slot->data = (uint8_t *)mapping + ((size_t)index * slot_size);
        slot->listener.release = buffer_release;
        slot->buffer = wl_shm_pool_create_buffer(
            pool,
            (int32_t)((size_t)index * slot_size),
            width,
            height,
            stride,
            WL_SHM_FORMAT_ARGB8888);
        if (slot->buffer == NULL) {
            set_error(window->host, "Wayland shared memory buffer creation failed.");
            wl_shm_pool_destroy(pool);
            retire_buffer_group(group);
            return NULL;
        }

        group->live_buffers++;
        wl_buffer_add_listener(slot->buffer, &slot->listener, slot);
    }

    wl_shm_pool_destroy(pool);
    return group;
}

static int32_t round_buffer_capacity(int32_t dimension) {
    if (dimension <= 0) {
        return ML_BUFFER_CAPACITY_STEP;
    }

    int64_t value = dimension;
    int64_t remainder = value % ML_BUFFER_CAPACITY_STEP;
    if (remainder != 0) {
        value += ML_BUFFER_CAPACITY_STEP - remainder;
    }

    if (value > INT32_MAX) {
        return INT32_MAX;
    }

    return (int32_t)value;
}

static ml_buffer_slot *acquire_buffer(
    ml_window *window,
    int width,
    int height,
    bool reusable) {
    ml_buffer_group *group = window->buffers;
    if (group == NULL || group->retiring ||
        (reusable
             ? width > group->width || height > group->height
             : group->width != width || group->height != height)) {
        retire_buffer_group(group);
        int buffer_width = reusable ? round_buffer_capacity(width) : width;
        int buffer_height = reusable ? round_buffer_capacity(height) : height;
        group = create_buffer_group(window, buffer_width, buffer_height);
    }

    if (group == NULL) {
        return NULL;
    }

    for (int index = 0; index < ML_BUFFER_COUNT; index++) {
        ml_buffer_slot *slot = &group->slots[index];
        if (!slot->busy && slot->buffer != NULL) {
            return slot;
        }
    }

    return NULL;
}

static void registry_listener_init(ml_host *host) {
    host->registry_listener.global = registry_global;
    host->registry_listener.global_remove = registry_global_remove;
    host->seat_listener.capabilities = seat_capabilities;
    host->seat_listener.name = seat_name;
    host->pointer_listener.enter = pointer_enter;
    host->pointer_listener.leave = pointer_leave;
    host->pointer_listener.motion = pointer_motion;
    host->pointer_listener.button = pointer_button;
    host->pointer_listener.axis = pointer_axis;
    host->pointer_listener.frame = pointer_frame;
    host->pointer_listener.axis_source = pointer_axis_source;
    host->pointer_listener.axis_stop = pointer_axis_stop;
    host->pointer_listener.axis_discrete = pointer_axis_discrete;
    host->pointer_listener.axis_value120 = pointer_axis_value120;
    host->pointer_listener.axis_relative_direction = pointer_axis_relative_direction;
    host->keyboard_listener.keymap = keyboard_keymap;
    host->keyboard_listener.enter = keyboard_enter;
    host->keyboard_listener.leave = keyboard_leave;
    host->keyboard_listener.key = keyboard_key;
    host->keyboard_listener.modifiers = keyboard_modifiers;
    host->keyboard_listener.repeat_info = keyboard_repeat_info;
}

static void destroy_outputs(ml_host *host) {
    ml_output *output = host->outputs;
    while (output != NULL) {
        ml_output *next = output->next;
        if (output->output != NULL) {
            wl_output_release(output->output);
        }
        free(output);
        output = next;
    }
    host->outputs = NULL;
}

void *ml_wayland_host_create_with_display(
    const ml_wayland_callbacks *callbacks,
    void *context,
    const char *display_name,
    void *native_display) {
    if (callbacks == NULL) {
        return NULL;
    }

    ml_global_error[0] = '\0';

    ml_host *host = calloc(1, sizeof(*host));
    if (host == NULL) {
        return NULL;
    }

    host->wakeup_read_fd = -1;
    host->wakeup_write_fd = -1;
    int wakeup_fds[2];
    if (pipe2(wakeup_fds, O_NONBLOCK | O_CLOEXEC) != 0) {
        set_errno_error(host, "Wayland wakeup pipe creation failed");
        free(host);
        return NULL;
    }
    host->wakeup_read_fd = wakeup_fds[0];
    host->wakeup_write_fd = wakeup_fds[1];

    host->callbacks = *callbacks;
    host->callback_context = context;
    if (native_display != NULL) {
        host->display = native_display;
        host->owns_display = false;
    } else {
        host->display = wl_display_connect(display_name);
        host->owns_display = true;
    }
    if (host->display == NULL) {
        set_error(host, "Wayland display connection failed. Set WAYLAND_DISPLAY.");
        ml_wayland_host_destroy(host);
        return NULL;
    }

    registry_listener_init(host);
    host->xkb_context = xkb_context_new(XKB_CONTEXT_NO_FLAGS);
    host->registry = wl_display_get_registry(host->display);
    if (host->registry == NULL) {
        set_error(host, "Wayland registry could not be created.");
        ml_wayland_host_destroy(host);
        return NULL;
    }

    wl_registry_add_listener(host->registry, &host->registry_listener, host);
    if (wl_display_roundtrip(host->display) < 0 ||
        wl_display_roundtrip(host->display) < 0) {
        set_error(host, "Wayland registry roundtrip failed.");
        ml_wayland_host_destroy(host);
        return NULL;
    }

    if (host->compositor == NULL || host->shm == NULL) {
        set_error(host, "Wayland compositor or shared memory is unavailable.");
        ml_wayland_host_destroy(host);
        return NULL;
    }

    host->decor = libdecor_new(host->display, &decoration_interface);
    if (host->decor == NULL) {
        set_error(host, "Wayland client-side decoration initialization failed.");
        ml_wayland_host_destroy(host);
        return NULL;
    }

    if (wl_display_roundtrip(host->display) < 0 ||
        wl_display_roundtrip(host->display) < 0) {
        set_error(host, "Wayland client-side decoration initialization failed.");
        ml_wayland_host_destroy(host);
        return NULL;
    }

    host->has_viewporter = host->viewporter != NULL;
    host->has_fractional_scale =
        host->fractional_scale_manager != NULL && host->has_viewporter;
    if (host->shm != NULL) {
        host->cursor_theme = wl_cursor_theme_load(
            getenv("XCURSOR_THEME"),
            ML_CURSOR_SIZE,
            host->shm);
    }

    return host;
}

void *ml_wayland_host_create(
    const ml_wayland_callbacks *callbacks,
    void *context,
    const char *display_name) {
    return ml_wayland_host_create_with_display(
        callbacks,
        context,
        display_name,
        NULL);
}

static void destroy_window_buffers(ml_window *window) {
    ml_buffer_group *group = window->buffers;
    while (group != NULL) {
        ml_buffer_group *next = group->next;
        destroy_buffer_group(group);
        free(group);
        group = next;
    }
    window->buffers = NULL;
}

static void destroy_window_surface(ml_window *window) {
    if (window->frame_callback != NULL) {
        wl_callback_destroy(window->frame_callback);
        window->frame_callback = NULL;
    }
    if (window->fractional_scale != NULL) {
        wp_fractional_scale_v1_destroy(window->fractional_scale);
        window->fractional_scale = NULL;
    }
    if (window->viewport != NULL) {
        wp_viewport_destroy(window->viewport);
        window->viewport = NULL;
    }
    if (window->frame != NULL) {
        libdecor_frame_unref(window->frame);
        window->frame = NULL;
    }
    if (window->surface != NULL) {
        wl_surface_destroy(window->surface);
        window->surface = NULL;
    }
}

void ml_wayland_window_destroy(void *host_pointer, uintptr_t window_pointer) {
    ml_host *host = host_pointer;
    ml_window *window = (ml_window *)window_pointer;
    if (host == NULL || window == NULL) {
        return;
    }

    ml_window **link = &host->windows;
    while (*link != NULL && *link != window) {
        link = &(*link)->next;
    }

    if (*link == window) {
        *link = window->next;
    }

    if (host->pointer_window == window) {
        host->pointer_window = NULL;
    }
    if (host->keyboard_window == window) {
        host->keyboard_window = NULL;
    }
    if (host->axis_window == window) {
        host->axis_window = NULL;
        host->axis_pending = false;
    }

    destroy_window_surface(window);
    wl_display_flush(host->display);
    if (host->display != NULL) {
        wl_display_roundtrip(host->display);
    }
    destroy_window_buffers(window);
    free(window);
    wl_display_flush(host->display);
}

void ml_wayland_host_destroy(void *host_pointer) {
    ml_host *host = host_pointer;
    if (host == NULL) {
        return;
    }

    memset(&host->callbacks, 0, sizeof(host->callbacks));
    ml_window *window = host->windows;
    while (window != NULL) {
        destroy_window_surface(window);
        window = window->next;
    }
    wl_display_flush(host->display);
    wl_display_roundtrip(host->display);

    window = host->windows;
    while (window != NULL) {
        ml_window *next = window->next;
        destroy_window_buffers(window);
        free(window);
        window = next;
    }
    host->windows = NULL;

    if (host->decor != NULL) {
        libdecor_unref(host->decor);
        host->decor = NULL;
    }

    if (host->pointer != NULL) {
        wl_pointer_release(host->pointer);
    }
    if (host->keyboard != NULL) {
        wl_keyboard_release(host->keyboard);
    }
    if (host->cursor_surface != NULL) {
        wl_surface_destroy(host->cursor_surface);
    }
    if (host->cursor_theme != NULL) {
        wl_cursor_theme_destroy(host->cursor_theme);
    }
    if (host->xkb_state != NULL) {
        xkb_state_unref(host->xkb_state);
    }
    if (host->xkb_keymap != NULL) {
        xkb_keymap_unref(host->xkb_keymap);
    }
    if (host->xkb_context != NULL) {
        xkb_context_unref(host->xkb_context);
    }
    if (host->seat != NULL) {
        wl_seat_release(host->seat);
    }
    if (host->fractional_scale_manager != NULL) {
        wp_fractional_scale_manager_v1_destroy(host->fractional_scale_manager);
    }
    if (host->viewporter != NULL) {
        wp_viewporter_destroy(host->viewporter);
    }
    if (host->compositor != NULL) {
        wl_compositor_destroy(host->compositor);
    }
    if (host->shm != NULL) {
        wl_shm_destroy(host->shm);
    }
    if (host->registry != NULL) {
        wl_registry_destroy(host->registry);
    }
    if (host->display != NULL) {
        wl_display_flush(host->display);
        if (host->owns_display) {
            wl_display_disconnect(host->display);
        }
    }
    if (host->wakeup_read_fd >= 0) {
        close(host->wakeup_read_fd);
        host->wakeup_read_fd = -1;
    }
    if (host->wakeup_write_fd >= 0) {
        close(host->wakeup_write_fd);
        host->wakeup_write_fd = -1;
    }
    destroy_outputs(host);
    free(host);
}

uintptr_t ml_wayland_window_create(
    void *host_pointer,
    uint64_t window_id,
    const char *title,
    int32_t logical_width,
    int32_t logical_height) {
    ml_host *host = host_pointer;
    if (host == NULL) {
        return 0;
    }

    ml_window *window = calloc(1, sizeof(*window));
    if (window == NULL) {
        set_error(host, "Wayland window allocation failed.");
        return 0;
    }

    window->host = host;
    window->window_id = window_id;
    window->logical_width = logical_width > 0 ? logical_width : 1;
    window->logical_height = logical_height > 0 ? logical_height : 1;
    window->scale = 1.0;
    window->surface_listener = surface_listener;
    window->fractional_scale_listener = fractional_scale_listener;
    window->surface = wl_compositor_create_surface(host->compositor);
    if (window->surface == NULL) {
        set_error(host, "Wayland surface creation failed.");
        free(window);
        return 0;
    }

    wl_surface_add_listener(window->surface, &window->surface_listener, window);

    if (host->has_fractional_scale) {
        window->fractional_scale =
            wp_fractional_scale_manager_v1_get_fractional_scale(
                host->fractional_scale_manager,
                window->surface);
        if (window->fractional_scale != NULL) {
            window->has_fractional_scale = true;
            wp_fractional_scale_v1_add_listener(
                window->fractional_scale,
                &window->fractional_scale_listener,
                window);
        }
    }

    if (host->has_viewporter) {
        window->viewport = wp_viewporter_get_viewport(host->viewporter, window->surface);
    }

    window->frame = libdecor_decorate(
        host->decor,
        window->surface,
        &frame_interface,
        window);
    if (window->frame == NULL) {
        set_error(host, "Wayland client-side decorated window creation failed.");
        ml_wayland_window_destroy(host, (uintptr_t)window);
        return 0;
    }

    libdecor_frame_set_title(
        window->frame,
        title != NULL ? title : "Melanopsin");
    libdecor_frame_set_app_id(window->frame, "dev.melanopsin.demo");

    window->next = host->windows;
    host->windows = window;
    libdecor_frame_map(window->frame);
    wl_display_flush(host->display);
    return (uintptr_t)window;
}

static bool dimensions_match_window(const ml_window *window, int32_t width, int32_t height) {
    int32_t expected_width = scaled_dimension(window->logical_width, window_scale(window));
    int32_t expected_height = scaled_dimension(window->logical_height, window_scale(window));
    return width == expected_width && height == expected_height;
}

int ml_wayland_window_can_present(
    void *host_pointer,
    uintptr_t window_pointer,
    int32_t width,
    int32_t height) {
    ml_host *host = host_pointer;
    ml_window *window = (ml_window *)window_pointer;
    if (host == NULL || window == NULL) {
        return -1;
    }

    if (width <= 0 || height <= 0) {
        set_error(host, "Wayland frame dimensions are invalid.");
        return -1;
    }

    if (window->closed || !window->configured || window->frame_callback != NULL) {
        return 0;
    }

    return dimensions_match_window(window, width, height) ? 1 : 0;
}

int ml_wayland_window_present(
    void *host_pointer,
    uintptr_t window_pointer,
    const uint8_t *pixels,
    int32_t width,
    int32_t height,
    int32_t stride,
    int32_t logical_width,
    int32_t logical_height) {
    ml_host *host = host_pointer;
    ml_window *window = (ml_window *)window_pointer;
    if (host == NULL || window == NULL || pixels == NULL) {
        return -1;
    }

    if (width <= 0 || height <= 0 || stride <= 0 || width > INT32_MAX / 4) {
        set_error(host, "Wayland frame dimensions or stride are invalid.");
        return -1;
    }

    // A frame callback is the Wayland-native backpressure mechanism. Do not
    // queue more frames while the compositor is still presenting the previous
    // one; the managed host keeps only the newest frame until this clears.
    int can_present = ml_wayland_window_can_present(
        host_pointer,
        window_pointer,
        width,
        height);
    if (can_present <= 0) {
        return can_present;
    }

    if (logical_width > 0 && logical_height > 0 &&
        (logical_width != window->logical_width ||
         logical_height != window->logical_height)) {
        // A configure change can make a pending managed frame stale before
        // the managed resize event is processed. Never let that stale frame
        // overwrite the compositor-owned logical size.
        return 0;
    }

    if (!dimensions_match_window(window, width, height)) {
        return 0;
    }

    if (stride < width * 4) {
        set_error(host, "Wayland frame stride is smaller than its width.");
        return -1;
    }

    ml_buffer_slot *slot = acquire_buffer(
        window,
        width,
        height,
        window->viewport != NULL);
    if (slot == NULL) {
        return 0;
    }

    size_t row_bytes = (size_t)width * 4;
    size_t destination_stride = (size_t)slot->group->stride;
    for (int32_t row = 0; row < height; row++) {
        memcpy(
            (uint8_t *)slot->data + ((size_t)row * destination_stride),
            pixels + ((size_t)row * (size_t)stride),
            row_bytes);
    }

    slot->busy = true;

    // Request the callback before committing so it is tied to this frame.
    // Compositors use this to pace clients; it also prevents flooding the
    // connection with attach/commit requests during an interactive resize.
    window->frame_callback = wl_surface_frame(window->surface);
    if (window->frame_callback != NULL) {
        wl_callback_add_listener(
            window->frame_callback,
            &frame_callback_listener,
            window);
    }

    wl_surface_attach(window->surface, slot->buffer, 0, 0);
    if (window->viewport != NULL) {
        wl_surface_set_buffer_scale(window->surface, 1);
        wp_viewport_set_source(
            window->viewport,
            wl_fixed_from_int(0),
            wl_fixed_from_int(0),
            wl_fixed_from_int(width),
            wl_fixed_from_int(height));
        wp_viewport_set_destination(
            window->viewport,
            window->logical_width,
            window->logical_height);
    } else {
        int32_t integer_scale = (int32_t)lround(window_scale(window));
        if (integer_scale < 1) {
            integer_scale = 1;
        }
        wl_surface_set_buffer_scale(window->surface, integer_scale);
    }

    wl_surface_damage_buffer(window->surface, 0, 0, width, height);
    wl_surface_commit(window->surface);
    if (wl_display_flush(host->display) < 0 && errno != EAGAIN) {
        set_errno_error(host, "Wayland frame commit flush failed");
        return -1;
    }

    return 1;
}

void ml_wayland_window_request_close(void *host_pointer, uintptr_t window_pointer) {
    ml_host *host = host_pointer;
    ml_window *window = (ml_window *)window_pointer;
    if (host == NULL || window == NULL) {
        return;
    }

    window->closed = true;
}

void ml_wayland_window_set_title(
    void *host_pointer,
    uintptr_t window_pointer,
    const char *title) {
    ml_host *host = host_pointer;
    ml_window *window = (ml_window *)window_pointer;
    if (host == NULL || window == NULL || window->frame == NULL ||
        title == NULL) {
        return;
    }

    libdecor_frame_set_title(window->frame, title);
}

void *ml_wayland_host_get_wl_display(void *host_pointer) {
    ml_host *host = host_pointer;
    return host == NULL ? NULL : host->display;
}

void *ml_wayland_window_get_wl_surface(uintptr_t window_pointer) {
    ml_window *window = (ml_window *)window_pointer;
    return window == NULL ? NULL : window->surface;
}

int ml_wayland_window_begin_egl_present(
    void *host_pointer,
    uintptr_t window_pointer,
    int32_t width,
    int32_t height) {
    ml_host *host = host_pointer;
    ml_window *window = (ml_window *)window_pointer;
    if (host == NULL || window == NULL) {
        return -1;
    }

    int can_present = ml_wayland_window_can_present(
        host_pointer,
        window_pointer,
        width,
        height);
    if (can_present <= 0) {
        return can_present;
    }

    if (!dimensions_match_window(window, width, height)) {
        return 0;
    }

    if (window->viewport != NULL) {
        wl_surface_set_buffer_scale(window->surface, 1);
        wp_viewport_set_source(
            window->viewport,
            wl_fixed_from_int(0),
            wl_fixed_from_int(0),
            wl_fixed_from_int(width),
            wl_fixed_from_int(height));
        wp_viewport_set_destination(
            window->viewport,
            window->logical_width,
            window->logical_height);
    } else {
        int32_t integer_scale = (int32_t)lround(window_scale(window));
        if (integer_scale < 1) {
            integer_scale = 1;
        }
        wl_surface_set_buffer_scale(window->surface, integer_scale);
    }

    return 1;
}

int ml_wayland_window_before_egl_swap(
    void *host_pointer,
    uintptr_t window_pointer) {
    ml_host *host = host_pointer;
    ml_window *window = (ml_window *)window_pointer;
    if (host == NULL || window == NULL || window->surface == NULL) {
        return -1;
    }

    if (window->frame_callback != NULL) {
        set_error(host, "Wayland EGL swap already has a frame callback.");
        return -1;
    }

    // Request the callback immediately before eglSwapBuffers. The EGL swap is
    // the implicit wl_surface.commit, so this ties backpressure to the buffer
    // that is actually being presented instead of an earlier decoration or
    // configure commit.
    window->frame_callback = wl_surface_frame(window->surface);
    if (window->frame_callback == NULL) {
        set_error(host, "Wayland could not request a frame callback.");
        return -1;
    }

    wl_callback_add_listener(
        window->frame_callback,
        &frame_callback_listener,
        window);
    return 1;
}

void ml_wayland_window_cancel_egl_present(
    void *host_pointer,
    uintptr_t window_pointer) {
    ml_host *host = host_pointer;
    ml_window *window = (ml_window *)window_pointer;
    if (host == NULL || window == NULL || window->frame_callback == NULL) {
        return;
    }

    wl_callback_destroy(window->frame_callback);
    window->frame_callback = NULL;
}

static int dispatch_wayland(ml_host *host, int timeout, const char *operation) {
    if (host == NULL || host->decor == NULL) {
        return -1;
    }

    // Drain anything libdecor has already queued before waiting. This keeps
    // ml_wayland_dispatch_pending() equivalent to the previous implementation
    // and prevents a notify callback from being lost when the compositor
    // also wrote a wakeup token.
    int result = libdecor_dispatch(host->decor, 0);
    if (result == -EINTR) {
        return 0;
    }
    if (result < 0) {
        errno = -result;
        set_errno_error(host, operation);
        return -1;
    }

    if (timeout == 0) {
        return 0;
    }

    int wayland_fd = libdecor_get_fd(host->decor);
    if (wayland_fd < 0) {
        errno = EIO;
        set_errno_error(host, operation);
        return -1;
    }

    struct pollfd descriptors[2];
    nfds_t descriptor_count = 0;
    int wayland_index = -1;
    int wakeup_index = -1;

    descriptors[descriptor_count].fd = wayland_fd;
    descriptors[descriptor_count].events = POLLIN;
    descriptors[descriptor_count].revents = 0;
    wayland_index = (int)descriptor_count++;

    if (host->wakeup_read_fd >= 0) {
        descriptors[descriptor_count].fd = host->wakeup_read_fd;
        descriptors[descriptor_count].events = POLLIN;
        descriptors[descriptor_count].revents = 0;
        wakeup_index = (int)descriptor_count++;
    }

    int poll_result;
    do {
        poll_result = poll(descriptors, descriptor_count, timeout);
    } while (poll_result < 0 && errno == EINTR);

    if (poll_result == 0) {
        return 0;
    }

    if (poll_result < 0) {
        set_errno_error(host, operation);
        return -1;
    }

    if (wakeup_index >= 0 &&
        (descriptors[wakeup_index].revents & POLLIN) != 0) {
        clear_wakeup(host);
    }

    short wayland_revents = wayland_index >= 0
        ? descriptors[wayland_index].revents
        : 0;
    if ((wayland_revents & (POLLIN | POLLERR | POLLHUP)) == 0) {
        return 0;
    }

    result = libdecor_dispatch(host->decor, 0);
    if (result == -EINTR) {
        return 0;
    }
    if (result < 0) {
        errno = -result;
        set_errno_error(host, operation);
        return -1;
    }

    return 0;
}

int ml_wayland_dispatch_pending(void *host_pointer) {
    return dispatch_wayland(
        host_pointer,
        0,
        "Wayland event dispatch failed");
}

int ml_wayland_wait_for_event(void *host_pointer, int32_t timeout_milliseconds) {
    return dispatch_wayland(
        host_pointer,
        timeout_milliseconds < 0 ? -1 : timeout_milliseconds,
        "Wayland wait failed");
}

int ml_wayland_flush(void *host_pointer) {
    ml_host *host = host_pointer;
    if (host == NULL) {
        return -1;
    }

    if (wl_display_flush(host->display) < 0 && errno != EAGAIN) {
        set_errno_error(host, "Wayland request flush failed");
        return -1;
    }

    return 0;
}

double ml_wayland_display_scale(void *host_pointer) {
    ml_host *host = host_pointer;
    if (host == NULL) {
        return 1.0;
    }

    int32_t scale = 1;
    for (ml_output *output = host->outputs; output != NULL; output = output->next) {
        if (output->scale > scale) {
            scale = output->scale;
        }
    }

    return (double)scale;
}

const char *ml_wayland_last_error(void *host_pointer) {
    ml_host *host = host_pointer;
    return host == NULL ? "Wayland host is unavailable." : host->error;
}

const char *ml_wayland_last_global_error(void) {
    return ml_global_error[0] == '\0' ? "Wayland operation failed." : ml_global_error;
}

void ml_wayland_clear_error(void *host_pointer) {
    ml_host *host = host_pointer;
    if (host != NULL) {
        host->error[0] = '\0';
    }
}
