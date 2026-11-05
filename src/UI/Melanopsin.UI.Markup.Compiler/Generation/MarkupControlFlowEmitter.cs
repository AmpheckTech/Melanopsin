using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Generation.Values;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Generation;

internal sealed class MarkupControlFlowEmitter {
    private readonly MarkupNodeEmitter _nodes;

    public MarkupControlFlowEmitter(MarkupNodeEmitter nodes) {
        _nodes = nodes;
    }

    internal void EmitIf(
        MarkupIf conditional,
        int indent,
        string? parentVariable,
        string? reconciliationKey) {
        _nodes.AppendLine(
            indent,
            CodeFragment.Text("if (")
                .Add(CodeFragment.Parenthesize(
                    CodeFragment.Source(conditional.Condition, conditional.ConditionOffset)))
                .Add(") {"));
        for (var index = 0; index < conditional.WhenTrue.Count; index++) {
            _nodes.Emit(
                conditional.WhenTrue[index],
                indent + 1,
                parentVariable,
                index == 0 ? reconciliationKey : null);
        }

        if (conditional.WhenFalse.Count > 0) {
            _nodes.AppendLine(indent, "} else {");
            for (var index = 0; index < conditional.WhenFalse.Count; index++) {
                _nodes.Emit(
                    conditional.WhenFalse[index],
                    indent + 1,
                    parentVariable,
                    index == 0 ? reconciliationKey : null);
            }

            _nodes.AppendLine(indent, "}");
            return;
        }

        _nodes.AppendLine(indent, "}");
    }

    internal void EmitFor(
        MarkupFor loop,
        int indent,
        string? parentVariable) {
        if (parentVariable is null) {
            throw new InvalidOperationException("@for requires a parent element.");
        }

        var hasItemsVariable = loop.EmptyBody.Count == 0
            ? null
            : $"__melanopsinForHasItems{loop.Offset}";
        if (hasItemsVariable is not null) {
            _nodes.AppendLine(indent, $"var {hasItemsVariable} = false;");
        }

        _nodes.AppendLine(
            indent,
            CodeFragment.Text($"foreach (var {loop.Variable} in ")
                .Add(CodeFragment.Parenthesize(
                    CodeFragment.Source(loop.Iterable, loop.IterableOffset)))
                .Add(") {"));

        // A single element body reconciles directly against the loop's parent, so
        // the iteration does not need a Fragment to group its roots.
        var singleRoot = loop.Body.Count == 1
            ? loop.Body[0] as MarkupElement
            : null;
        if (singleRoot is not null) {
            var rootVariable = _nodes.Emit(
                singleRoot,
                indent + 1,
                parentVariable,
                reconciliationKey: null);
            if (loop.Track is { Length: > 0 } && rootVariable is not null) {
                _nodes.AppendLine(
                    indent + 1,
                    CodeFragment.Text($"{rootVariable}.ReconciliationKey = (object?)")
                        .Add(CodeFragment.Parenthesize(
                            CodeFragment.Source(loop.Track, loop.TrackOffset)))
                        .Add(";"));
            }
        } else {
            var fragmentVariable = _nodes.NextVariable("__melanopsinFragment");
            _nodes.AppendLine(
                indent + 1,
                $"var {fragmentVariable} = new global::Melanopsin.UI.Fragment();");
            _nodes.AppendLine(
                indent + 1,
                $"{fragmentVariable}.ReconciliationSlot = {loop.Offset};");
            if (loop.Track is { Length: > 0 }) {
                _nodes.AppendLine(
                    indent + 1,
                    CodeFragment.Text($"{fragmentVariable}.ReconciliationKey = (object?)")
                        .Add(CodeFragment.Parenthesize(
                            CodeFragment.Source(loop.Track, loop.TrackOffset)))
                        .Add(";"));
            }

            _nodes.AppendLine(
                indent + 1,
                $"{parentVariable}.AppendChild({fragmentVariable});");
            foreach (var child in loop.Body) {
                _nodes.Emit(
                    child,
                    indent + 1,
                    fragmentVariable,
                    reconciliationKey: null);
            }
        }

        if (hasItemsVariable is not null) {
            _nodes.AppendLine(indent + 1, $"{hasItemsVariable} = true;");
        }

        _nodes.AppendLine(indent, "}");

        if (hasItemsVariable is null) {
            return;
        }

        _nodes.AppendLine(indent, $"if (!{hasItemsVariable}) {{");
        var emptyFragmentVariable = _nodes.NextVariable("__melanopsinEmptyFragment");
        _nodes.AppendLine(
            indent + 1,
            $"var {emptyFragmentVariable} = new global::Melanopsin.UI.Fragment();");
        _nodes.AppendLine(
            indent + 1,
            $"{emptyFragmentVariable}.ReconciliationSlot = {loop.Offset};");
        _nodes.AppendLine(
            indent + 1,
            $"{parentVariable}.AppendChild({emptyFragmentVariable});");
        foreach (var emptyNode in loop.EmptyBody) {
            _nodes.Emit(
                emptyNode,
                indent + 1,
                emptyFragmentVariable,
                reconciliationKey: null);
        }

        _nodes.AppendLine(indent, "}");
    }

    internal void EmitSwitch(
        MarkupSwitch switchBlock,
        int indent,
        string? parentVariable) {
        _nodes.AppendLine(
            indent,
            CodeFragment.Text("switch (")
                .Add(CodeFragment.Parenthesize(
                    CodeFragment.Source(switchBlock.Expression, switchBlock.ExpressionOffset)))
                .Add(") {"));
        foreach (var switchCase in switchBlock.Cases) {
            EmitSwitchSection(
                switchCase.Body,
                CodeFragment.Text("case ")
                    .Add(CodeFragment.Source(switchCase.Pattern, switchCase.PatternOffset))
                    .Add(":"),
                indent,
                parentVariable);
        }

        if (switchBlock.DefaultBody.Count != 0) {
            EmitSwitchSection(
                switchBlock.DefaultBody,
                CodeFragment.Text("default:"),
                indent,
                parentVariable);
        }

        _nodes.AppendLine(indent, "}");
    }

    private void EmitSwitchSection(
        IReadOnlyList<MarkupNode> body,
        CodeFragment label,
        int indent,
        string? parentVariable) {
        _nodes.AppendLine(indent, label);
        _nodes.AppendLine(indent, "{");
        foreach (var child in body) {
            _nodes.Emit(
                child,
                indent + 1,
                parentVariable,
                reconciliationKey: null);
        }

        _nodes.AppendLine(indent, "    break;");
        _nodes.AppendLine(indent, "}");
    }
}
