using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Identity;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Catalog.Errors;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;

namespace Shopilent.Application.Features.Catalog.Commands.CreateAttribute.V1;

internal sealed class CreateAttributeCommandHandlerV1 : ICommandHandler<CreateAttributeCommandV1, CreateAttributeResponseV1>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttributeWriteRepository _attributeWriteRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<CreateAttributeCommandHandlerV1> _logger;

    public CreateAttributeCommandHandlerV1(
        IUnitOfWork unitOfWork,
        IAttributeWriteRepository attributeWriteRepository,
        ICurrentUserContext currentUserContext,
        ILogger<CreateAttributeCommandHandlerV1> logger)
    {
        _unitOfWork = unitOfWork;
        _attributeWriteRepository = attributeWriteRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<Result<CreateAttributeResponseV1>> Handle(CreateAttributeCommandV1 request, CancellationToken cancellationToken)
    {
        try
        {
            // Check if name already exists
            var nameExists = await _attributeWriteRepository.NameExistsAsync(request.Name, null, cancellationToken);
            if (nameExists)
            {
                return Result.Failure<CreateAttributeResponseV1>(AttributeErrors.DuplicateName(request.Name));
            }

            // Create attribute
            var attributeResult = Domain.Catalog.Attribute.Create(request.Name, request.DisplayName, request.Type);
            if (attributeResult.IsFailure)
            {
                return Result.Failure<CreateAttributeResponseV1>(attributeResult.Error);
            }

            var attribute = attributeResult.Value;

            // Set properties based on request
            if (request.Filterable)
            {
                attribute.SetFilterable(true);
            }

            if (request.Searchable)
            {
                attribute.SetSearchable(true);
            }

            if (request.IsVariant)
            {
                attribute.SetIsVariant(true);
            }

            // Add configuration if provided
            if (request.Configuration != null)
            {
                foreach (var item in request.Configuration)
                {
                    attribute.UpdateConfiguration(item.Key, item.Value);
                }
            }

            // Set audit info if user context is available
            if (_currentUserContext.UserId.HasValue)
            {
                attribute.SetCreationAuditInfo(_currentUserContext.UserId);
            }

            // Add to repository
            await _attributeWriteRepository.AddAsync(attribute, cancellationToken);

            // Save changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Create response
            var response = new CreateAttributeResponseV1
            {
                Id = attribute.Id,
                Name = attribute.Name,
                DisplayName = attribute.DisplayName,
                Type = attribute.Type,
                Filterable = attribute.Filterable,
                Searchable = attribute.Searchable,
                IsVariant = attribute.IsVariant,
                Configuration = attribute.Configuration,
                CreatedAt = attribute.CreatedAt
            };

            _logger.LogInformation("Attribute created successfully with ID: {AttributeId}", attribute.Id);

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating attribute: {ErrorMessage}", ex.Message);

            return Result.Failure<CreateAttributeResponseV1>(
                Error.Failure(
                    "Attribute.CreateFailed",
                    "Failed to create attribute"));
        }
    }
}
