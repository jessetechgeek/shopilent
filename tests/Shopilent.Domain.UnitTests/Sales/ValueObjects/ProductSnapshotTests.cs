using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.ValueObjects;

namespace Shopilent.Domain.Tests.Sales.ValueObjects;

public class ProductSnapshotTests
{
    [Fact]
    public void Create_WithValidName_ShouldCreateProductSnapshot()
    {
        // Arrange
        var name = "Test Product";

        // Act
        var result = ProductSnapshot.Create(name);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.Name.Should().Be(name);
        snapshot.Sku.Should().BeNull();
        snapshot.Slug.Should().BeNull();
        snapshot.VariantSku.Should().BeNull();
        snapshot.VariantAttributes.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllParameters_ShouldCreateProductSnapshotWithAllFields()
    {
        // Arrange
        var name = "Test Product";
        var sku = "PROD-001";
        var slug = "test-product";
        var variantSku = "VAR-001";
        var variantAttributes = new Dictionary<string, object> { { "Color", "Red" }, { "Size", "Large" } };

        // Act
        var result = ProductSnapshot.Create(name, sku, slug, variantSku, variantAttributes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.Name.Should().Be(name);
        snapshot.Sku.Should().Be(sku);
        snapshot.Slug.Should().Be(slug);
        snapshot.VariantSku.Should().Be(variantSku);
        snapshot.VariantAttributes.Should().BeEquivalentTo(variantAttributes);
    }

    [Fact]
    public void Create_WithNullName_ShouldReturnFailure()
    {
        // Arrange
        string name = null;

        // Act
        var result = ProductSnapshot.Create(name);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductSnapshotErrors.NameRequired);
    }

    [Fact]
    public void Create_WithEmptyName_ShouldReturnFailure()
    {
        // Arrange
        var name = string.Empty;

        // Act
        var result = ProductSnapshot.Create(name);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductSnapshotErrors.NameRequired);
    }

    [Fact]
    public void Create_WithWhitespaceName_ShouldReturnFailure()
    {
        // Arrange
        var name = "   ";

        // Act
        var result = ProductSnapshot.Create(name);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductSnapshotErrors.NameRequired);
    }

    [Fact]
    public void Create_WithNullSku_ShouldSucceed()
    {
        // Arrange
        var name = "Test Product";
        string sku = null;

        // Act
        var result = ProductSnapshot.Create(name, sku);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.Name.Should().Be(name);
        snapshot.Sku.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptySku_ShouldSucceed()
    {
        // Arrange
        var name = "Test Product";
        var sku = string.Empty;

        // Act
        var result = ProductSnapshot.Create(name, sku);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.Name.Should().Be(name);
        snapshot.Sku.Should().Be(sku);
    }

    [Fact]
    public void Create_WithEmptyVariantAttributes_ShouldCreateSnapshotWithEmptyDictionary()
    {
        // Arrange
        var name = "Test Product";
        var variantAttributes = new Dictionary<string, object>();

        // Act
        var result = ProductSnapshot.Create(name, variantAttributes: variantAttributes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.VariantAttributes.Should().NotBeNull();
        snapshot.VariantAttributes.Should().BeEmpty();
    }

    [Fact]
    public void ToDictionary_WithOnlyName_ShouldReturnDictionaryWithName()
    {
        // Arrange
        var name = "Test Product";
        var snapshot = ProductSnapshot.Create(name).Value;

        // Act
        var dict = snapshot.ToDictionary();

        // Assert
        dict.Should().ContainKey("name");
        dict["name"].Should().Be(name);
        dict.Should().NotContainKey("sku");
        dict.Should().NotContainKey("slug");
        dict.Should().NotContainKey("variant_sku");
        dict.Should().NotContainKey("variant_attributes");
    }

    [Fact]
    public void ToDictionary_WithAllFields_ShouldReturnCompleteDictionary()
    {
        // Arrange
        var name = "Test Product";
        var sku = "PROD-001";
        var slug = "test-product";
        var variantSku = "VAR-001";
        var variantAttributes = new Dictionary<string, object> { { "Color", "Red" }, { "Size", "Large" } };
        var snapshot = ProductSnapshot.Create(name, sku, slug, variantSku, variantAttributes).Value;

        // Act
        var dict = snapshot.ToDictionary();

        // Assert
        dict.Should().ContainKey("name");
        dict["name"].Should().Be(name);
        dict.Should().ContainKey("sku");
        dict["sku"].Should().Be(sku);
        dict.Should().ContainKey("slug");
        dict["slug"].Should().Be(slug);
        dict.Should().ContainKey("variant_sku");
        dict["variant_sku"].Should().Be(variantSku);
        dict.Should().ContainKey("variant_attributes");
        dict["variant_attributes"].Should().BeEquivalentTo(variantAttributes);
    }

    [Fact]
    public void ToDictionary_WithEmptySku_ShouldNotIncludeSkuInDictionary()
    {
        // Arrange
        var name = "Test Product";
        var sku = string.Empty;
        var snapshot = ProductSnapshot.Create(name, sku).Value;

        // Act
        var dict = snapshot.ToDictionary();

        // Assert
        dict.Should().ContainKey("name");
        dict.Should().NotContainKey("sku");
    }

    [Fact]
    public void ToDictionary_WithWhitespaceSku_ShouldNotIncludeSkuInDictionary()
    {
        // Arrange
        var name = "Test Product";
        var sku = "   ";
        var snapshot = ProductSnapshot.Create(name, sku).Value;

        // Act
        var dict = snapshot.ToDictionary();

        // Assert
        dict.Should().ContainKey("name");
        dict.Should().NotContainKey("sku");
    }

    [Fact]
    public void ToDictionary_WithEmptyVariantAttributes_ShouldNotIncludeVariantAttributesInDictionary()
    {
        // Arrange
        var name = "Test Product";
        var variantAttributes = new Dictionary<string, object>();
        var snapshot = ProductSnapshot.Create(name, variantAttributes: variantAttributes).Value;

        // Act
        var dict = snapshot.ToDictionary();

        // Assert
        dict.Should().ContainKey("name");
        dict.Should().NotContainKey("variant_attributes");
    }

    [Fact]
    public void FromDictionary_WithValidData_ShouldCreateProductSnapshot()
    {
        // Arrange
        var dict = new Dictionary<string, object>
        {
            { "name", "Test Product" },
            { "sku", "PROD-001" },
            { "slug", "test-product" },
            { "variant_sku", "VAR-001" },
            { "variant_attributes", new Dictionary<string, object> { { "Color", "Red" }, { "Size", "Large" } } }
        };

        // Act
        var result = ProductSnapshot.FromDictionary(dict);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.Name.Should().Be("Test Product");
        snapshot.Sku.Should().Be("PROD-001");
        snapshot.Slug.Should().Be("test-product");
        snapshot.VariantSku.Should().Be("VAR-001");
        snapshot.VariantAttributes.Should().NotBeNull();
        snapshot.VariantAttributes.Should().ContainKey("Color");
    }

    [Fact]
    public void FromDictionary_WithOnlyName_ShouldCreateProductSnapshotWithNameOnly()
    {
        // Arrange
        var dict = new Dictionary<string, object> { { "name", "Test Product" } };

        // Act
        var result = ProductSnapshot.FromDictionary(dict);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.Name.Should().Be("Test Product");
        snapshot.Sku.Should().BeNull();
        snapshot.Slug.Should().BeNull();
        snapshot.VariantSku.Should().BeNull();
        snapshot.VariantAttributes.Should().BeNull();
    }

    [Fact]
    public void FromDictionary_WithNullDictionary_ShouldReturnFailure()
    {
        // Arrange
        Dictionary<string, object> dict = null;

        // Act
        var result = ProductSnapshot.FromDictionary(dict);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductSnapshotErrors.InvalidData);
    }

    [Fact]
    public void FromDictionary_WithMissingName_ShouldReturnFailure()
    {
        // Arrange
        var dict = new Dictionary<string, object> { { "sku", "PROD-001" }, { "slug", "test-product" } };

        // Act
        var result = ProductSnapshot.FromDictionary(dict);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductSnapshotErrors.NameRequired);
    }

    [Fact]
    public void FromDictionary_WithNullName_ShouldReturnFailure()
    {
        // Arrange
        var dict = new Dictionary<string, object> { { "name", null }, { "sku", "PROD-001" } };

        // Act
        var result = ProductSnapshot.FromDictionary(dict);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductSnapshotErrors.NameRequired);
    }

    [Fact]
    public void FromDictionary_ThenToDictionary_ShouldRoundTripSuccessfully()
    {
        // Arrange
        var originalDict = new Dictionary<string, object>
        {
            { "name", "Test Product" },
            { "sku", "PROD-001" },
            { "slug", "test-product" },
            { "variant_sku", "VAR-001" },
            { "variant_attributes", new Dictionary<string, object> { { "Color", "Red" }, { "Size", "Large" } } }
        };

        // Act
        var snapshot = ProductSnapshot.FromDictionary(originalDict).Value;
        var resultDict = snapshot.ToDictionary();

        // Assert
        resultDict.Should().ContainKey("name");
        resultDict["name"].Should().Be(originalDict["name"]);
        resultDict.Should().ContainKey("sku");
        resultDict["sku"].Should().Be(originalDict["sku"]);
        resultDict.Should().ContainKey("slug");
        resultDict["slug"].Should().Be(originalDict["slug"]);
        resultDict.Should().ContainKey("variant_sku");
        resultDict["variant_sku"].Should().Be(originalDict["variant_sku"]);
        resultDict.Should().ContainKey("variant_attributes");
    }

    [Fact]
    public void FromDictionary_WithPartialData_ShouldCreateSnapshotWithAvailableFields()
    {
        // Arrange
        var dict = new Dictionary<string, object>
        {
            { "name", "Test Product" }, { "sku", "PROD-001" }
            // Missing slug, variant_sku, variant_attributes
        };

        // Act
        var result = ProductSnapshot.FromDictionary(dict);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.Name.Should().Be("Test Product");
        snapshot.Sku.Should().Be("PROD-001");
        snapshot.Slug.Should().BeNull();
        snapshot.VariantSku.Should().BeNull();
        snapshot.VariantAttributes.Should().BeNull();
    }

    [Fact]
    public void FromDictionary_WithNonStringValues_ShouldConvertToString()
    {
        // Arrange
        var dict = new Dictionary<string, object>
        {
            { "name", 12345 }, // Non-string value
            { "sku", 67890 } // Non-string value
        };

        // Act
        var result = ProductSnapshot.FromDictionary(dict);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.Name.Should().Be("12345");
        snapshot.Sku.Should().Be("67890");
    }

    [Fact]
    public void FromDictionary_WithNullVariantAttributes_ShouldCreateSnapshotWithNullVariantAttributes()
    {
        // Arrange
        var dict = new Dictionary<string, object> { { "name", "Test Product" }, { "variant_attributes", null } };

        // Act
        var result = ProductSnapshot.FromDictionary(dict);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value;
        snapshot.VariantAttributes.Should().BeNull();
    }
}
