using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Events;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Domain.Catalog;

public class Category : AggregateRoot
{
    private Category()
    {
        // Required by EF Core
    }

    private Category(string name, Slug slug)
    {
        Name = name;
        Slug = slug;
        ParentId = null;
        Level = 0;
        Path = $"/{slug}";
        IsActive = true;
        _productCategories = new List<ProductCategory>();
    }

    public static Result<Category> Create(string name, Slug slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Category>(CategoryErrors.NameRequired);

        if (slug == null || string.IsNullOrWhiteSpace(slug.Value))
            return Result.Failure<Category>(CategoryErrors.SlugRequired);

        var category = new Category(name, slug);
        category.AddDomainEvent(new CategoryCreatedEvent(category.Id));
        return Result.Success(category);
    }

    public static Result<Category> CreateInactive(string name, Slug slug)
    {
        var result = Create(name, slug);
        if (result.IsFailure)
            return result;

        var category = result.Value;
        category.IsActive = false;
        return Result.Success(category);
    }

    public string Name { get; private set; }
    public string Description { get; private set; }
    public Guid? ParentId { get; private set; }
    public Slug Slug { get; private set; }
    public int Level { get; private set; }
    public string Path { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<ProductCategory> _productCategories = new();
    public IReadOnlyCollection<ProductCategory> ProductCategories => _productCategories.AsReadOnly();

    public Result Update(string name, Slug slug, string description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(CategoryErrors.NameRequired);

        if (slug == null || string.IsNullOrWhiteSpace(slug.Value))
            return Result.Failure(CategoryErrors.SlugRequired);

        Name = name;
        Slug = slug;
        Description = description;
        AddDomainEvent(new CategoryUpdatedEvent(Id));
        return Result.Success();
    }

    public Result Activate()
    {
        if (IsActive)
            return Result.Success();

        IsActive = true;
        AddDomainEvent(new CategoryStatusChangedEvent(Id, true));
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Success();

        IsActive = false;
        AddDomainEvent(new CategoryStatusChangedEvent(Id, false));
        return Result.Success();
    }

    /// <summary>
    /// Sets the category hierarchy values after they've been computed in the Application layer.
    /// This method should be called after loading the parent category to compute Level and Path.
    /// </summary>
    /// <param name="parentId">The parent category ID, or null for root categories</param>
    /// <param name="level">The computed hierarchy level (0 for root, parent.Level + 1 for children)</param>
    /// <param name="path">The computed path (e.g., "/electronics/computers")</param>
    public void SetHierarchy(Guid? parentId, int level, string path)
    {
        ParentId = parentId;
        Level = level;
        Path = path;
    }

    /// <summary>
    /// Sets the parent category ID only.
    /// Call SetHierarchy() separately to update Level and Path after computing in Application layer.
    /// </summary>
    /// <param name="parentId">The parent category ID, or null to make this a root category</param>
    public Result SetParent(Guid? parentId)
    {
        if (ParentId == parentId)
            return Result.Success(); // No change

        ParentId = parentId;

        // Level and Path will be set separately via SetHierarchy()
        // after Application layer loads parent and computes values

        AddDomainEvent(new CategoryHierarchyChangedEvent(Id));
        return Result.Success();
    }
    
    public Result Delete()
    {
        if (_productCategories.Any())
            return Result.Failure(CategoryErrors.CannotDeleteWithProducts);

        // Children check moved to Application layer
        AddDomainEvent(new CategoryDeletedEvent(Id));
        return Result.Success();
    }
}