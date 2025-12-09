import {ProductAttributeDto} from '@/models/catalog';

// Define the input attribute type more explicitly
type InputAttribute = ProductAttributeDto | { attributeId: string; value: any };

// Define the normalized output type
type NormalizedAttribute = { attributeId: string; value: any };

/**
 * Normalizes product attributes by extracting the value from nested structure if needed
 *
 * The backend API sometimes returns attributes with a nested structure like:
 * { attributeId: "123", values: { value: "someValue" } }
 *
 * This function normalizes it to:
 * { attributeId: "123", value: "someValue" }
 */
export function normalizeProductAttributes(attributes: InputAttribute[]): NormalizedAttribute[] {
  return attributes.map(attr => {
    // If it already has a value property, return as is
    if ('value' in attr) {
      return {
        attributeId: attr.attributeId,
        value: attr.value
      };
    }

    // At this point, we know it's a ProductAttributeDto with 'values' property
    const productAttr = attr as ProductAttributeDto;

    // If it has a values property with a nested value, extract it
    if (productAttr.values && typeof productAttr.values === 'object' && 'value' in productAttr.values) {
      return {
        attributeId: productAttr.attributeId,
        value: productAttr.values.value
      };
    }

    // If it has a values property but no nested value, use the values directly
    if (productAttr.values) {
      return {
        attributeId: productAttr.attributeId,
        value: productAttr.values
      };
    }

    // Fallback for unexpected structure
    return {
      attributeId: productAttr.attributeId,
      value: null
    };
  });
}

/**
 * Prepares attribute data for API submission by wrapping the value in a values object if needed
 *
 * Converts from:
 * { attributeId: "123", value: "someValue" }
 *
 * To:
 * { attributeId: "123", values: { value: "someValue" } }
 */
export function prepareAttributesForSubmit(attributes: NormalizedAttribute[]): {
  attributeId: string;
  values: { value: any }
}[] {
  return attributes.map(attr => ({
    attributeId: attr.attributeId,
    values: {value: attr.value}
  }));
}

/**
 * Formats an attribute value for display, handling objects and arrays correctly
 *
 * This is useful for showing attribute values in UI components
 */
export function formatAttributeValue(value: any): string {
  if (value === null || value === undefined) {
    return '';
  }

  if (typeof value === 'object') {
    // Try to find meaningful properties to display
    if ('name' in value) return value.name;
    if ('label' in value) return value.label;
    if ('value' in value) return formatAttributeValue(value.value);

    // For arrays, join the values
    if (Array.isArray(value)) {
      return value.map(formatAttributeValue).join(', ');
    }

    // Fall back to JSON stringify but with better formatting
    try {
      return JSON.stringify(value)
        .replace(/[{}"\[\]]/g, '')  // Remove JSON syntax chars
        .replace(/,/g, ', ');       // Add space after commas
    } catch (e) {
      return String(value);
    }
  }

  return String(value);
}
