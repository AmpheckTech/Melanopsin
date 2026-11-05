// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI;
using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Demo.Views;

[MarkupComponent]
public sealed partial class MotionSample : Component {
    private bool _showDetails;

    public bool ShowDetails => _showDetails;

    public string DetailsButtonLabel => _showDetails ? "Hide notes" : "Show notes";

    public string MotionSpec => "720 ms · ease out · alternate";

    public string Message =>
        "A single repeating Tween owns the bounce. Its repaint boundary keeps the surrounding " +
        "interface stable, while the panel samples the moving content through a backdrop blur.";

    public void ToggleDetails() {
        _showDetails = !_showDetails;
    }
}
