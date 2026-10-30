using Melanopsin.Graphics;

namespace Melanopsin.UI.Hosting;

public readonly record struct WindowDescriptor {
    public WindowDescriptor(WindowId id, string title, PixelSize size) {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        Id = id;
        Title = title;
        Size = size;
    }

    public WindowId Id { get; }

    public string Title { get; }

    public PixelSize Size { get; }
}
