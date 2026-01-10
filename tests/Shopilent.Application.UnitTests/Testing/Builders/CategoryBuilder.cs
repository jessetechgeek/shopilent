using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;

namespace Shopilent.Application.UnitTests.Testing.Builders;

/// <summary>
/// Builder pattern for creating Category entities for tests
/// </summary>
public class CategoryBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Category";
    private string _slug = "test-category";
    private string _description = "Test category description";
    private Guid? _parentId = null;
    private Category _parentCategory = null;
    private int _level = 0;
    private string _path = "/test-category";
    private bool _isActive = true;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;

    public CategoryBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public CategoryBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CategoryBuilder WithSlug(string slug)
    {
        _slug = slug;
        return this;
    }

    public CategoryBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public CategoryBuilder WithParent(Category parent)
    {
        _parentCategory = parent;
        _parentId = parent.Id;
        _level = parent.Level + 1;
        _path = $"{parent.Path}/{_slug}";
        return this;
    }

    public CategoryBuilder WithParentId(Guid parentId)
    {
        _parentId = parentId;
        _level = 1; // Assuming level 1 when just setting parent ID
        _path = $"/unknown-parent/{_slug}";
        return this;
    }

    public CategoryBuilder IsInactive()
    {
        _isActive = false;
        return this;
    }

    public CategoryBuilder CreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public Category Build()
    {
        // Create the slug value object
        var slugResult = Slug.Create(_slug);
        if (slugResult.IsFailure)
            throw new InvalidOperationException($"Invalid slug: {_slug}");

        var categoryResult = Category.Create(_name, slugResult.Value);

        if (categoryResult.IsFailure)
            throw new InvalidOperationException($"Failed to create category: {categoryResult.Error.Message}");

        var category = categoryResult.Value;

        // Set hierarchy if parent exists
        if (_parentCategory != null)
        {
            category.SetHierarchy(_parentCategory.Id, _parentCategory.Level + 1, $"{_parentCategory.Path}/{slugResult.Value}");
        }
        else if (_parentId.HasValue)
        {
            // Use the stored hierarchy values
            category.SetHierarchy(_parentId, _level, _path);
        }

        // Use reflection to set the ID, created date, etc.
        SetPrivatePropertyValue(category, "Id", _id);
        SetPrivatePropertyValue(category, "CreatedAt", _createdAt);
        SetPrivatePropertyValue(category, "UpdatedAt", _updatedAt);

        // Add description if needed
        if (!string.IsNullOrEmpty(_description))
        {
            category.Update(category.Name, category.Slug, _description);
        }

        // Set inactive if needed
        if (!_isActive)
        {
            category.Deactivate();
        }

        return category;
    }

    private static void SetPrivatePropertyValue<T>(object obj, string propertyName, T value)
    {
        var propertyInfo = obj.GetType().GetProperty(propertyName);
        if (propertyInfo != null)
        {
            propertyInfo.SetValue(obj, value, null);
        }
        else
        {
            var fieldInfo = obj.GetType().GetField(propertyName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(obj, value);
            }
            else
            {
                throw new InvalidOperationException($"Property or field {propertyName} not found on type {obj.GetType().Name}");
            }
        }
    }
}
