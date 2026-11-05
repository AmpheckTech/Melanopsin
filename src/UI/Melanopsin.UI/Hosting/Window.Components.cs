using System.Runtime.ExceptionServices;
using Melanopsin.UI.App;

namespace Melanopsin.UI.Hosting;

public sealed partial class Window {
    private void AttachRuntimeToTree(Node root) {
        var runtime = _runtime ?? throw new InvalidOperationException(
            "A window runtime must be attached before propagating it to a root tree.");
        AttachRuntimeToTree(root, runtime);
    }

    private static List<ComponentHost>? AttachRuntimeToTree(
        Node node,
        UiRuntime runtime) {
        var hosts = new List<ComponentHost>();
        CollectComponentHosts(node, hosts);
        foreach (var host in hosts) {
            if (host.Runtime is not null && !ReferenceEquals(host.Runtime, runtime)) {
                throw new InvalidOperationException(
                    "A component host cannot be attached to more than one UiRuntime.");
            }
        }

        List<ComponentHost>? attached = null;
        foreach (var host in hosts) {
            if (host.Runtime is null) {
                host.AttachRuntime(runtime);
                (attached ??= []).Add(host);
            }
        }

        return attached;
    }

    private static void CollectComponentHosts(Node node, List<ComponentHost> hosts) {
        if (node is ComponentHost host) {
            hosts.Add(host);
        }

        if (node is not Element element) {
            return;
        }

        foreach (var child in element.Children) {
            CollectComponentHosts(child, hosts);
        }
    }

    private static void DetachRuntimes(
        IReadOnlyList<ComponentHost>? hosts,
        UiRuntime? runtime,
        List<Exception> failures) {
        if (hosts is null || runtime is null) {
            return;
        }

        for (var index = hosts.Count - 1; index >= 0; index--) {
            try {
                hosts[index].DetachRuntime(runtime);
            } catch (Exception exception) {
                failures.Add(exception);
            }
        }
    }

    private static void ThrowIfCleanupFailed(
        string message,
        List<Exception> failures) {
        if (failures.Count > 1) {
            throw new AggregateException(message, failures);
        }

        ExceptionDispatchInfo.Capture(failures[0]).Throw();
    }
}
