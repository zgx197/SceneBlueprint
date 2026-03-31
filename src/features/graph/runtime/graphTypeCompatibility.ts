export interface GraphTypeCompatibilityRegistry {
  registerImplicitConversion(sourceType: string, targetType: string): void;
  isCompatible(sourceType?: string, targetType?: string): boolean;
}

export const GRAPH_ANY_DATA_TYPE = "any";

export function createGraphTypeCompatibilityRegistry(): GraphTypeCompatibilityRegistry {
  const conversions = new Map<string, Set<string>>();

  return {
    registerImplicitConversion(sourceType, targetType) {
      const set = conversions.get(sourceType) ?? new Set<string>();
      set.add(targetType);
      conversions.set(sourceType, set);
    },
    isCompatible(sourceType, targetType) {
      if (!sourceType || !targetType) {
        return true;
      }

      if (sourceType === targetType) {
        return true;
      }

      if (sourceType === GRAPH_ANY_DATA_TYPE || targetType === GRAPH_ANY_DATA_TYPE) {
        return true;
      }

      return conversions.get(sourceType)?.has(targetType) ?? false;
    },
  };
}
