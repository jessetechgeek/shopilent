using FluentAssertions;
using Moq;
using Shopilent.Application.Features.Catalog.Queries.GetAttributesDatatable.V1;
using Shopilent.Application.UnitTests.Common;
using Shopilent.Domain.Catalog.DTOs;
using Shopilent.Domain.Catalog.Enums;
using Shopilent.Domain.Common.Models;

namespace Shopilent.Application.UnitTests.Features.Catalog.Queries.V1;

public class GetAttributesDatatableQueryV1Tests : TestBase
{
    private readonly GetAttributesDatatableQueryHandlerV1 _handler;

    public GetAttributesDatatableQueryV1Tests()
    {
        _handler = new GetAttributesDatatableQueryHandlerV1(
            Fixture.MockAttributeReadRepository.Object,
            Fixture.GetLogger<GetAttributesDatatableQueryHandlerV1>());
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsFormattedDatatableResult()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10,
            Search = new DataTableSearch { Value = "color" },
            Order = new List<DataTableOrder>
            {
                new DataTableOrder { Column = 0, Dir = "asc" }
            },
            Columns = new List<DataTableColumn>
            {
                new DataTableColumn { Data = "name", Name = "Name", Searchable = true, Orderable = true }
            }
        };

        var query = new GetAttributesDatatableQueryV1
        {
            Request = request
        };

        var attributes = new List<AttributeDto>
        {
            new AttributeDto
            {
                Id = Guid.NewGuid(),
                Name = "Color",
                DisplayName = "Product Color",
                Type = AttributeType.Text,
                Filterable = true,
                Searchable = true,
                IsVariant = false,
                Configuration = new Dictionary<string, object> { { "required", true } },
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new AttributeDto
            {
                Id = Guid.NewGuid(),
                Name = "Size",
                DisplayName = "Product Size",
                Type = AttributeType.Select,
                Filterable = true,
                Searchable = false,
                IsVariant = true,
                Configuration = new Dictionary<string, object> { { "options", new[] { "S", "M", "L" } } },
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow
            }
        };

        var datatableResult = new DataTableResult<AttributeDto>(
            draw: 1,
            recordsTotal: 2,
            recordsFiltered: 2,
            data: attributes);

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetDataTableAsync(request, CancellationToken))
            .ReturnsAsync(datatableResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Draw.Should().Be(1);
        result.Value.RecordsTotal.Should().Be(2);
        result.Value.RecordsFiltered.Should().Be(2);
        result.Value.Data.Count.Should().Be(2);

        var firstAttribute = result.Value.Data.First();
        firstAttribute.Name.Should().Be("Color");
        firstAttribute.DisplayName.Should().Be("Product Color");
        firstAttribute.Type.Should().Be("Text");
        firstAttribute.Filterable.Should().BeTrue();
        firstAttribute.Searchable.Should().BeTrue();
        firstAttribute.IsVariant.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithEmptyResult_ReturnsEmptyDatatableResult()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10
        };

        var query = new GetAttributesDatatableQueryV1
        {
            Request = request
        };

        var datatableResult = new DataTableResult<AttributeDto>(
            draw: 1,
            recordsTotal: 0,
            recordsFiltered: 0,
            data: new List<AttributeDto>());

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetDataTableAsync(request, CancellationToken))
            .ReturnsAsync(datatableResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Draw.Should().Be(1);
        result.Value.RecordsTotal.Should().Be(0);
        result.Value.RecordsFiltered.Should().Be(0);
        result.Value.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_ReturnsFailureResult()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10
        };

        var query = new GetAttributesDatatableQueryV1
        {
            Request = request
        };

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetDataTableAsync(request, CancellationToken))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Attributes.GetDataTableFailed");
        result.Error.Message.Should().Contain("Test exception");
    }

    [Fact]
    public async Task Handle_VerifiesCorrectMappingFromAttributeToDto()
    {
        // Arrange
        var request = new DataTableRequest
        {
            Draw = 1,
            Start = 0,
            Length = 10
        };

        var query = new GetAttributesDatatableQueryV1
        {
            Request = request
        };

        var sourceAttribute = new AttributeDto
        {
            Id = Guid.NewGuid(),
            Name = "Weight",
            DisplayName = "Product Weight",
            Type = AttributeType.Number,
            Filterable = false,
            Searchable = true,
            IsVariant = false,
            Configuration = new Dictionary<string, object>
            {
                { "unit", "kg" },
                { "precision", 2 }
            },
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var datatableResult = new DataTableResult<AttributeDto>(
            draw: 1,
            recordsTotal: 1,
            recordsFiltered: 1,
            data: new List<AttributeDto> { sourceAttribute });

        Fixture.MockAttributeReadRepository
            .Setup(repo => repo.GetDataTableAsync(request, CancellationToken))
            .ReturnsAsync(datatableResult);

        // Act
        var result = await _handler.Handle(query, CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var mappedAttribute = result.Value.Data.First();
        mappedAttribute.Id.Should().Be(sourceAttribute.Id);
        mappedAttribute.Name.Should().Be(sourceAttribute.Name);
        mappedAttribute.DisplayName.Should().Be(sourceAttribute.DisplayName);
        mappedAttribute.Type.Should().Be("Number");
        mappedAttribute.Filterable.Should().Be(sourceAttribute.Filterable);
        mappedAttribute.Searchable.Should().Be(sourceAttribute.Searchable);
        mappedAttribute.IsVariant.Should().Be(sourceAttribute.IsVariant);
        mappedAttribute.Configuration.Should().BeEquivalentTo(sourceAttribute.Configuration);
        mappedAttribute.CreatedAt.Should().Be(sourceAttribute.CreatedAt);
        mappedAttribute.UpdatedAt.Should().Be(sourceAttribute.UpdatedAt);
    }
}
