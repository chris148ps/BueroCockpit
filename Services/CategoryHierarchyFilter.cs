using BueroCockpit.Models;

namespace BueroCockpit.Services;

public static class CategoryHierarchyFilter
{
    public static HashSet<string> GetCategoryAndDescendantIds(
        IEnumerable<CategoryItem> categories,
        string categoryId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return result;
        }

        var categoryList = categories.ToList();
        var pending = new Queue<string>();
        result.Add(categoryId);
        pending.Enqueue(categoryId);

        while (pending.Count > 0)
        {
            var currentId = pending.Dequeue();
            foreach (var child in categoryList.Where(category =>
                         string.Equals(category.ParentId, currentId, StringComparison.OrdinalIgnoreCase)))
            {
                if (result.Add(child.Id))
                {
                    pending.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    public static bool Matches(
        TaskItem task,
        IEnumerable<CategoryItem> categories,
        string categoryId)
    {
        var acceptedCategoryIds = GetCategoryAndDescendantIds(categories, categoryId);
        return GetTaskCategoryIds(task).Any(acceptedCategoryIds.Contains);
    }

    public static bool IsArchived(
        TaskItem task,
        IEnumerable<CategoryItem> categories)
    {
        if (task.Status.Equals("Archiv", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var archiveCategoryIds = categories
            .Where(category => category.Name.Trim().Equals("Archiv", StringComparison.OrdinalIgnoreCase))
            .Select(category => category.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return GetTaskCategoryIds(task).Any(archiveCategoryIds.Contains);
    }

    public static bool IsVisibleInNormalCategory(
        TaskItem task,
        IEnumerable<CategoryItem> categories,
        string categoryId)
    {
        var categoryList = categories.ToList();
        return !task.IsDeleted &&
               !IsArchived(task, categoryList) &&
               Matches(task, categoryList, categoryId);
    }

    private static IEnumerable<string> GetTaskCategoryIds(TaskItem task)
    {
        if (!string.IsNullOrWhiteSpace(task.CategoryId))
        {
            yield return task.CategoryId;
        }

        foreach (var categoryId in task.CategoryIds)
        {
            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                yield return categoryId;
            }
        }
    }
}
