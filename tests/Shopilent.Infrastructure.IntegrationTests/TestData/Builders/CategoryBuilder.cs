using Bogus;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.ValueObjects;

namespace Shopilent.Infrastructure.IntegrationTests.TestData.Builders;

public class CategoryBuilder
{
    private string _name;
    private string _description;
    private Slug _slug;
    private Category _parentCategory;
    private bool _isActive;

    public CategoryBuilder()
    {
        var faker = new Faker();
        _name = faker.Commerce.Department();
        _description = faker.Lorem.Sentence(10);
        
        // Generate unique slug by appending timestamp to avoid collisions
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var baseSlug = _name.ToLower().Replace(" ", "-").Replace("&", "and");
        _slug = Slug.Create($"{baseSlug}-{timestamp}").Value;
        
        _parentCategory = null;
        _isActive = true;
    }

    public CategoryBuilder WithName(string name)
    {
        _name = name;
        // Update slug when name changes to ensure uniqueness
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var baseSlug = name.ToLower().Replace(" ", "-").Replace("&", "and");
        _slug = Slug.Create($"{baseSlug}-{timestamp}").Value;
        return this;
    }

    public CategoryBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public CategoryBuilder WithSlug(string slug)
    {
        _slug = Slug.Create(slug).Value;
        return this;
    }

    public CategoryBuilder WithParentCategory(Category parentCategory)
    {
        _parentCategory = parentCategory;
        return this;
    }

    public CategoryBuilder WithoutParent()
    {
        _parentCategory = null;
        return this;
    }

    public CategoryBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public CategoryBuilder AsActive()
    {
        _isActive = true;
        return this;
    }

    public Category Build()
    {
        var category = Category.Create(_name, _slug).Value;

        // Set hierarchy if parent exists
        if (_parentCategory != null)
        {
            category.SetHierarchy(
                _parentCategory.Id,
                _parentCategory.Level + 1,
                $"{_parentCategory.Path}/{_slug.Value}");
        }

        category.Update(_name, _slug, _description);

        if (!_isActive)
        {
            category.Deactivate();
        }

        return category;
    }

    public static CategoryBuilder Random()
    {
        return new CategoryBuilder();
    }

    public static List<Category> CreateMany(int count)
    {
        var categories = new List<Category>();
        var faker = new Faker();
        
        for (int i = 0; i < count; i++)
        {
            categories.Add(Random().Build());
        }
        
        return categories;
    }
}