// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;
using Melanopsin.Mathematics;
using Melanopsin.UI.Animation;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Demo.Views;

[MarkupElement]
public sealed class BouncingBall :
    Control,
    IAnimationParticipant,
    IRenderCommandProvider {
    private static readonly Color BallColor = new(31, 109, 89);
    private static readonly Color ShadowColor = new(24, 54, 45);
    private static readonly Color HighlightColor = new(255, 255, 255, 92);
    private readonly Tween<float> _bounce = new(
        from: 0,
        to: 1,
        duration: TimeSpan.FromMilliseconds(720),
        interpolate: static (from, to, progress) =>
            from + ((to - from) * progress),
        easing: Easing.EaseOut,
        iterations: -1,
        direction: AnimationDirection.Alternate);

    public BouncingBall()
        : base("BouncingBall") {
        ClipToBounds = true;
        IsRepaintBoundary = true;
    }

    protected override void OnMounted() {
        base.OnMounted();
        _bounce.Play();
    }

    protected override void OnUnmounted() {
        _bounce.Pause();
        base.OnUnmounted();
    }

    bool IAnimationParticipant.AdvanceAnimation(float elapsedSeconds) {
        if (_bounce.Advance(elapsedSeconds)) {
            InvalidatePaint();
        }

        return _bounce.IsPlaying;
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        var bounds = context.Bounds;
        var padding = context.Padding;
        var lift = Math.Clamp(_bounce.Value, 0, 1);
        var contact = 1 - lift;
        var squash = contact * contact * contact;
        var diameter = MathF.Max(
            18,
            MathF.Min(
                context.FontSize * 3,
                MathF.Min(bounds.Width * 0.24f, bounds.Height * 0.24f)));
        var ballWidth = diameter * (1 + (0.12f * squash));
        var ballHeight = diameter * (1 - (0.10f * squash));
        var groundY = bounds.MaxY - padding;
        var travel = MathF.Max(0, bounds.Height - (padding * 2) - diameter);
        var ballX = bounds.MinX + ((bounds.Width - ballWidth) / 2);
        var ballY = groundY - ballHeight - (travel * lift);

        var guide = new Color(25, 32, 29);
        var guideX = bounds.MinX + (bounds.Width / 2);
        commands.Add(new FillRectangleCommand(
            new Rect(guideX, bounds.MinY + padding, 1, groundY - bounds.MinY - padding),
            guide));
        commands.Add(new FillRectangleCommand(
            new Rect(bounds.MinX + padding, groundY, bounds.Width - (padding * 2), 1),
            new Color(42, 52, 47)));

        for (var index = 1; index < 4; index++) {
            var tickX = bounds.MinX + (bounds.Width * index / 4);
            commands.Add(new FillRectangleCommand(
                new Rect(tickX, groundY - 3, 1, 7),
                guide));
        }

        var shadowWidth = ballWidth * (1 - (0.34f * lift));
        var shadowHeight = MathF.Max(2, diameter * 0.09f);
        var shadowAlpha = (byte)(48 - (34 * lift));
        commands.Add(new FillRectangleCommand(
            new Rect(
                bounds.MinX + ((bounds.Width - shadowWidth) / 2),
                groundY - (shadowHeight / 2),
                shadowWidth,
                shadowHeight),
            new Color(
                ShadowColor.Red,
                ShadowColor.Green,
                ShadowColor.Blue,
                shadowAlpha),
            shadowHeight / 2));

        commands.Add(new FillRectangleCommand(
            new Rect(ballX, ballY, ballWidth, ballHeight),
            BallColor,
            ballHeight / 2));

        var highlightSize = diameter * 0.16f;
        commands.Add(new FillRectangleCommand(
            new Rect(
                ballX + (ballWidth * 0.24f),
                ballY + (ballHeight * 0.18f),
                highlightSize,
                highlightSize),
            HighlightColor,
            highlightSize / 2));
    }
}
