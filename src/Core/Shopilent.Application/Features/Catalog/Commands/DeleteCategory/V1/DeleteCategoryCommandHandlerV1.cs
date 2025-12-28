using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.DeleteCategory.V1;

internal sealed class DeleteCategoryCommandHandlerV1 : ICommandHandler<DeleteCategoryCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductReadRepository _productReadRepository;
    private readonly ICategoryWriteRepository _categoryWriteRepository;
    private readonly ICategoryReadRepository _categoryReadRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<DeleteCategoryCommandHandlerV1> _logger;

    public DeleteCategoryCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IProductReadRepository productReadRepository,
        ICategoryWriteRepository categoryWriteRepository,
        ICategoryReadRepository categoryReadRepository,
        ICurrentUserContext currentUserContext,
        ILogger<DeleteCategoryCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _productReadRepository = productReadRepository;
        _categoryWriteRepository = categoryWriteRepository;
        _categoryReadRepository = categoryReadRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteCategoryCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get category by ID
            var category = await _categoryWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (category == null)
            {
                return Result.Failure(CategoryErrors.NotFound(request.Id));
            }

            // Check if category has child categories
            var childCategories =
                await _categoryReadRepository.GetChildCategoriesAsync(request.Id, cancellationToken);
            if (childCategories != null && childCategories.Count > 0)
            {
                return Result.Failure(CategoryErrors.CannotDeleteWithChildren);
            }

            // Check if category has associated products
            var products = await _productReadRepository.GetByCategoryAsync(request.Id, cancellationToken);
            if (products != null && products.Count > 0)
            {
                return Result.Failure(CategoryErrors.CannotDeleteWithProducts);
            }

            // Delete category
            await _categoryWriteRepository.DeleteAsync(category, cancellationToken);

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Category deleted successfully with ID: {CategoryId}", category.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category with ID {CategoryId}: {ErrorMessage}", request.Id,
                ex.Message);

            return Result.Failure(
                Error.Failure(
                    "Category.DeleteFailed",
                    "Failed to delete category"
                ));
        }
    }
}
