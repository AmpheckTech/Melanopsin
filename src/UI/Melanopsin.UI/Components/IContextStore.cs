namespace Melanopsin.UI;

internal interface IContextStore {
    Dictionary<object, object>? ContextValues { get; set; }
}
