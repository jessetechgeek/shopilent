using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Catalog.ValueObjects;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.CreateCategory.V1;

internal sealed class
    CreateCategoryCommandHandlerV1 : ICommandHandler<CreateCategoryCommandV1, CreateCategoryResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryWriteRepository _categoryWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<CreateCategoryCommandHandlerV1> _logger;

    public CreateCategoryCommandHandlerV1(
        IUnitOfWork unitOfWork,
        ICategoryWriteRepository categoryWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<CreateCategoryCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _categoryWriteRepository = categoryWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<CreateCategoryResponseV1>> Handle(CreateCategoryCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check if slug already exists
            var slugExists = await _categoryWriteRepository.SlugExistsAsync(request.Slug, null, cancellationToken);
            if (slugExists)
            {
                return Result.Failure<CreateCategoryResponseV1>(CategoryErrors.DuplicateSlug(request.Slug));
            }

            // Create slug value object
            var slugResult = Slug.Create(request.Slug);
            if (slugResult.IsFailure)
            {
                return Result.Failure<CreateCategoryResponseV1>(slugResult.Error);
            }

            // Get parent category if provided
            Category parentCategory = null;
            if (request.ParentId.HasValue)
            {
                parentCategory =
                    await _categoryWriteRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
                if (parentCategory == null)
                {
                    return Result.Failure<CreateCategoryResponseV1>(CategoryErrors.NotFound(request.ParentId.Value));
                }
            }

            // Create category
            var categoryResult = Category.Create(request.Name, slugResult.Value, parentCategory);
            if (categoryResult.IsFailure)
            {
                return Result.Failure<CreateCategoryResponseV1>(categoryResult.Error);
            }

            var category = categoryResult.Value;

            // Set description if provided
            if (!string.IsNullOrEmpty(request.Description))
            {
                category.Update(category.Name, category.Slug, request.Description);
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                category.SetCreationAuditInfo(_currentUserContext.UserId);
            }

            // Add to repository
            await _categoryWriteRepository.AddAsync(category, cancellationToken);

            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            // Create response
            var response = new CreateCategoryResponseV1
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug.Value,
                Description = category.Description,
                ParentId = category.ParentId,
                Level = category.Level,
                Path = category.Path,
                IsActive = category.IsActive,
                CreatedAt = category.CreatedAt
            };

            _logger.LogInformation("Category created successfully with ID: {CategoryId}", category.Id);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category: {ErrorMessage}", ex.Message);

            return Result.Failure<CreateCategoryResponseV1>(
                Error.Failure(
                    "Category.CreateFailed",
                    Domain.Common.Errors.ErrorType.Failure.ToString()
                ));
        }
    }
}
