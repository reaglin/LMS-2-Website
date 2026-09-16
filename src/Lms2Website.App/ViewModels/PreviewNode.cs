using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lms2Website.Core.Model;

namespace Lms2Website.App.ViewModels;

/// <summary>
/// One line of the preview tree: a section, a sub-heading inside it, or an item. Each line has a
/// tick, and the tick is the truth — what is ticked here is what the website will contain.
///
/// A parent's tick is three-state and derived: ticked when every child is, cleared when none is,
/// and indeterminate in between. Ticking a parent ticks everything under it. The model item each
/// leaf stands for is updated as the tick changes, so <see cref="SiteBuilder"/> needs to know
/// nothing about this class — it already publishes only what is <c>Include</c>d.
/// </summary>
public sealed class PreviewNode : INotifyPropertyChanged
{
    public string Title { get; init; } = string.Empty;
    /// <summary>"Quiz · 26 questions", "3 pages, 1 assignment" — the right-hand detail.</summary>
    public string Detail { get; init; } = string.Empty;
    public bool IsSection { get; init; }
    public bool IsExpanded { get; init; }
    public List<PreviewNode> Children { get; } = new();

    /// <summary>The section this line stands for, when it is a section.</summary>
    private SiteModule? _module;
    /// <summary>The item this line stands for, when it is a leaf.</summary>
    private SiteItem? _item;
    private PreviewNode? _parent;

    /// <summary>An item the cartridge carries but the app cannot publish cannot be ticked on.</summary>
    public bool CanInclude { get; private init; } = true;

    private bool? _isIncluded = true;
    /// <summary>Three-state: true, false, or null for a parent whose children disagree.</summary>
    public bool? IsIncluded
    {
        get => _isIncluded;
        set
        {
            // A three-state box cycles to null on click; for a parent that means "make it all off".
            var wanted = value ?? false;
            if (_isIncluded == wanted && Children.Count == 0) return;

            SetSelfAndChildren(wanted);
            _parent?.RefreshFromChildren();
        }
    }

    private void SetSelfAndChildren(bool included)
    {
        if (CanInclude)
        {
            _isIncluded = included;
            if (_module != null) _module.Include = included;
            if (_item != null) _item.Include = included;
        }
        else
        {
            _isIncluded = false;
        }

        foreach (var child in Children) child.SetSelfAndChildren(included);
        Raise(nameof(IsIncluded));
    }

    private void RefreshFromChildren()
    {
        if (Children.Count == 0) return;

        var states = Children.Where(c => c.CanInclude).Select(c => c.IsIncluded).Distinct().ToList();
        _isIncluded = states.Count == 0 ? false
                    : states.Count == 1 ? states[0]
                    : null;

        // A section with nothing ticked inside it is itself not published.
        if (_module != null) _module.Include = _isIncluded != false;

        Raise(nameof(IsIncluded));
        _parent?.RefreshFromChildren();
    }

    public static List<PreviewNode> Build(CourseSite course)
    {
        var nodes = new List<PreviewNode>();
        foreach (var module in course.Modules)
        {
            var section = new PreviewNode
            {
                Title = module.Title,
                Detail = module.Summary(),
                IsSection = true,
                IsExpanded = course.Modules.Count <= 3,
                _module = module,
                _isIncluded = module.Include
            };

            PreviewNode? group = null;
            foreach (var item in module.Items)
            {
                var publishable = item.Kind != ItemKind.Unsupported;
                var leaf = new PreviewNode
                {
                    Title = item.Title,
                    Detail = publishable
                        ? item.Describe()
                        : "not published" + (item.Notes.Count > 0 ? " · " + string.Join(" · ", item.Notes) : ""),
                    CanInclude = publishable,
                    _item = publishable ? item : null,
                    _isIncluded = publishable && item.Include
                };

                if (string.IsNullOrEmpty(item.Section))
                {
                    leaf._parent = section;
                    section.Children.Add(leaf);
                    continue;
                }
                if (group == null || group.Title != item.Section)
                {
                    group = new PreviewNode { Title = item.Section, IsExpanded = true, Detail = string.Empty };
                    group._parent = section;
                    section.Children.Add(group);
                }
                leaf._parent = group;
                group.Children.Add(leaf);
            }

            foreach (var child in section.Children) child.RefreshFromChildren();
            section.RefreshFromChildren();
            nodes.Add(section);
        }
        return nodes;
    }

    /// <summary>Everything in the tree, so the window can count what is ticked.</summary>
    public IEnumerable<PreviewNode> Descend()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var node in child.Descend())
                yield return node;
    }

    /// <summary>True when this line stands for a real item that is ticked.</summary>
    public bool IsIncludedItem => _item != null && _isIncluded == true;

    /// <summary>True when this line stands for a real item at all.</summary>
    public bool IsItem => _item != null;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
