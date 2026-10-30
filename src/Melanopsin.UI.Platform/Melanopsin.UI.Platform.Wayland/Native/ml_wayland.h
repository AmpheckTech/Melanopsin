#ifndef MELANOPSIN_WAYLAND_H
#define MELANOPSIN_WAYLAND_H

#include <stdint.h>

#if defined(__cplusplus)
extern "C" {
#endif

typedef void (*ml_configure_callback)(
    void *context,
    uintptr_t window,
    int32_t logical_width,
    int32_t logical_height,
    double scale);
typedef void (*ml_close_callback)(void *context, uintptr_t window);
typedef void (*ml_pointer_callback)(
    void *context,
    uintptr_t window,
    uint32_t phase,
    double x,
    double y,
    uint32_t button,
    uint32_t timestamp);
typedef void (*ml_axis_callback)(
    void *context,
    uintptr_t window,
    double horizontal,
    double vertical,
    int32_t horizontal_value120,
    int32_t vertical_value120,
    uint32_t timestamp);
typedef void (*ml_key_callback)(
    void *context,
    uintptr_t window,
    uint32_t keysym,
    uint32_t state,
    uint32_t modifiers,
    const char *text,
    uint32_t timestamp);
typedef void (*ml_focus_callback)(void *context, uintptr_t window, int32_t focused);
typedef void (*ml_error_callback)(void *context, const char *message);

typedef struct ml_wayland_callbacks {
    ml_configure_callback configure;
    ml_close_callback close;
    ml_pointer_callback pointer;
    ml_axis_callback axis;
    ml_key_callback key;
    ml_focus_callback focus;
    ml_error_callback error;
} ml_wayland_callbacks;

void *ml_wayland_host_create(
    const ml_wayland_callbacks *callbacks,
    void *context,
    const char *display_name);
void *ml_wayland_host_create_with_display(
    const ml_wayland_callbacks *callbacks,
    void *context,
    const char *display_name,
    void *native_display);
void ml_wayland_host_destroy(void *host);
void *ml_wayland_host_get_wl_display(void *host);

uintptr_t ml_wayland_window_create(
    void *host,
    uint64_t window_id,
    const char *title,
    int32_t logical_width,
    int32_t logical_height);
void ml_wayland_window_destroy(void *host, uintptr_t window);
int ml_wayland_window_can_present(
    void *host,
    uintptr_t window,
    int32_t width,
    int32_t height);
int ml_wayland_window_present(
    void *host,
    uintptr_t window,
    const uint8_t *pixels,
    int32_t width,
    int32_t height,
    int32_t stride,
    int32_t logical_width,
    int32_t logical_height);
void ml_wayland_window_request_close(void *host, uintptr_t window);
void ml_wayland_window_set_title(
    void *host,
    uintptr_t window,
    const char *title);
void *ml_wayland_window_get_wl_surface(uintptr_t window);
int ml_wayland_window_begin_egl_present(
    void *host,
    uintptr_t window,
    int32_t width,
    int32_t height);
int ml_wayland_window_before_egl_swap(
    void *host,
    uintptr_t window);
void ml_wayland_window_cancel_egl_present(void *host, uintptr_t window);

int ml_wayland_dispatch_pending(void *host);
int ml_wayland_wait_for_event(void *host, int32_t timeout_milliseconds);
int ml_wayland_flush(void *host);
double ml_wayland_display_scale(void *host);
const char *ml_wayland_last_error(void *host);
const char *ml_wayland_last_global_error(void);
void ml_wayland_clear_error(void *host);

#if defined(__cplusplus)
}
#endif

#endif
