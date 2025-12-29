using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.UpdateAttribute.V1;

internal sealed class
    UpdateAttributeCommandHandlerV1 : ICommandHandler<UpdateAttributeCommandV1, UpdateAttributeResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttributeWriteRepository _attributeWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<UpdateAttributeCommandHandlerV1> _logger;

    public UpdateAttributeCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IAttributeWriteRepository attributeWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<UpdateAttributeCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _attributeWriteRepository = attributeWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<UpdateAttributeResponseV1>> Handle(UpdateAttributeCommandV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get attribute by ID
            var attribute = await _attributeWriteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (attribute == null)
            {
                return Result.Failure<UpdateAttributeResponseV1>(AttributeErrors.NotFound(request.Id));
            }

            attribute.Update(request.DisplayName);

            // Update filterable property if different from current
            if (attribute.Filterable != request.Filterable)
            {
                attribute.SetFilterable(request.Filterable);
            }

            // Update searchable property if different from current
            if (attribute.Searchable != request.Searchable)
            {
                attribute.SetSearchable(request.Searchable);
            }

            // Update isVariant property if different from current
            if (attribute.IsVariant != request.IsVariant)
            {
                attribute.SetIsVariant(request.IsVariant);
            }

            // Update configuration if provided
            if (request.Configuration != null)
            {
                // Create a new dictionary with the updated configuration
                var newConfiguration = new Dictionary<string, object>(request.Configuration);

                // Replace the entire configuration with the new one instead of updating individual keys
                attribute.Configuration.Clear();

                // Add each key-value pair from the new configuration
                foreach (var item in newConfiguration)
                {
                    attribute.UpdateConfiguration(item.Key, item.Value);
                }
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                attribute.SetAuditInfo(_currentUserContext.UserId);
            }

            await _attributeWriteRepository.UpdateAsync(attribute, cancellationToken);


            // Save changes
            await _unitOfWork.CommitAsync(cancellationToken);

            // Create response
            var response = new UpdateAttributeResponseV1
            {
                Id = attribute.Id,
                Name = attribute.Name,
                DisplayName = attribute.DisplayName,
                Type = attribute.Type,
                Filterable = attribute.Filterable,
                Searchable = attribute.Searchable,
                IsVariant = attribute.IsVariant,
                Configuration = attribute.Configuration,
                UpdatedAt = attribute.UpdatedAt
            };

            _logger.LogInformation("Attribute updated successfully with ID: {AttributeId}", attribute.Id);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attribute with ID {AttributeId}: {ErrorMessage}",
                request.Id, ex.Message);

            return Result.Failure<UpdateAttributeResponseV1>(
                Error.Failure(
                    "Attribute.UpdateFailed",
                    "Failed to update attribute"));
        }
    }
}
