using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.DeleteAttribute.V1;

internal sealed class DeleteAttributeCommandHandlerV1 : ICommandHandler<DeleteAttributeCommandV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttributeWriteRepository _attributeWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<DeleteAttributeCommandHandlerV1> _logger;

    public DeleteAttributeCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IAttributeWriteRepository attributeWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<DeleteAttributeCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _attributeWriteRepository = attributeWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteAttributeCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Get attribute by ID
            var attribute = await _attributeWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (attribute == null)
            {
                return Result.Failure(AttributeErrors.NotFound(request.Id));
            }

            // Check if attribute is used by any products
            var productsWithAttribute = await _unitOfWork.ProductReader.SearchAsync($"attribute:{attribute.Name}", cancellationToken: cancellationToken);
            if (productsWithAttribute != null && productsWithAttribute.Count > 0)
            {
                return Result.Failure(
                    Error.Conflict(
                        code: "Attribute.InUse",
                        message: $"Cannot delete attribute '{attribute.Name}' because it is used by {productsWithAttribute.Count} products"));
            }

            // Call domain delete method to raise AttributeDeletedEvent
            var deleteResult = attribute.Delete();
            if (deleteResult.IsFailure)
            {
                return deleteResult;
            }

            // Delete attribute from repository
            await _attributeWriteRepository.DeleteAsync(attribute, cancellationToken);

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Attribute deleted successfully with ID: {AttributeId}", attribute.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attribute with ID {AttributeId}: {ErrorMessage}", request.Id,
                ex.Message);

            return Result.Failure(
                Error.Failure(
                    "Attribute.DeleteFailed",
                    "Failed to delete attribute"
                ));
        }
    }
}
