using Lms2Website.Core.Model;

namespace Lms2Website.App.ViewModels;

/// <summary>
/// One line of the preview tree: a section, a sub-heading inside it, or an item. Read-only —
/// it shows exactly what the site will contain, built once when a cartridge is read.
/// </summary>
public sealed class PreviewNode
{
    public string Title { get; init; } = string.Empty;
    /// <summary>"Quiz · 26 questions", "3 pages, 1 assignment" — the right-hand detail.</summary>
    public string Detail { get; init; } = string.Empty;
    public bool IsSection { get; init; }
    public bool IsExpanded { get; init; }
    public List<PreviewNode> Children { get; } = new();

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
                IsExpanded = course.Modules.Count <= 3
            };

            PreviewNode? group = null;
            foreach (var item in module.Items)
            {
                var leaf = new PreviewNode
                {
                    Title = item.Title,
                    Detail = item.Kind == ItemKind.Unsupported
                        ? "not published" + (item.Notes.Count > 0 ? " · " + string.Join(" · ", item.Notes) : "")
                        : item.Describe()
                };

                if (string.IsNullOrEmpty(item.Section))
                {
                    section.Children.Add(leaf);
                    continue;
                }
                if (group == null || group.Title != item.Section)
                {
                    group = new PreviewNode { Title = item.Section, IsExpanded = true, Detail = string.Empty };
                    section.Children.Add(group);
                }
                group.Children.Add(leaf);
            }

            nodes.Add(section);
        }
        return nodes;
    }
}
