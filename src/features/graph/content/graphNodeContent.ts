export type GraphNodeContentFieldKind = "text" | "number" | "boolean" | "select" | "readonly";

export interface GraphNodeContentSelectOption {
  value: string;
  label: string;
}

interface GraphNodeContentFieldBase {
  key: string;
  label: string;
  description?: string;
  readValue?: (payload: Record<string, unknown>) => unknown;
}

export interface GraphNodeTextFieldDefinition extends GraphNodeContentFieldBase {
  kind: "text";
  placeholder?: string;
}

export interface GraphNodeNumberFieldDefinition extends GraphNodeContentFieldBase {
  kind: "number";
  min?: number;
  max?: number;
  step?: number;
}

export interface GraphNodeBooleanFieldDefinition extends GraphNodeContentFieldBase {
  kind: "boolean";
}

export interface GraphNodeSelectFieldDefinition extends GraphNodeContentFieldBase {
  kind: "select";
  options: GraphNodeContentSelectOption[];
}

export interface GraphNodeReadonlyFieldDefinition extends GraphNodeContentFieldBase {
  kind: "readonly";
  placeholder?: string;
}

export type GraphNodeContentFieldDefinition =
  | GraphNodeTextFieldDefinition
  | GraphNodeNumberFieldDefinition
  | GraphNodeBooleanFieldDefinition
  | GraphNodeSelectFieldDefinition
  | GraphNodeReadonlyFieldDefinition;

export interface GraphNodeContentSectionDefinition {
  id: string;
  title: string;
  fields: GraphNodeContentFieldDefinition[];
}

export interface GraphNodeContentLine {
  key: string;
  label: string;
  value: string;
}

export interface GraphNodeContentProjection {
  summaryText?: string;
  detailLines: GraphNodeContentLine[];
}

export interface GraphNodeContentDefinition {
  description?: string;
  sections: GraphNodeContentSectionDefinition[];
  buildProjection(payload: Record<string, unknown>): GraphNodeContentProjection;
}

export type GraphNodeContentDraftValue = string | boolean;

export function readGraphNodePayloadRecord(payload: unknown): Record<string, unknown> {
  return typeof payload === "object" && payload !== null && !Array.isArray(payload)
    ? (payload as Record<string, unknown>)
    : {};
}

export function readGraphNodeContentFieldValue(
  field: GraphNodeContentFieldDefinition,
  payload: Record<string, unknown>,
): unknown {
  if (field.readValue) {
    return field.readValue(payload);
  }

  return payload[field.key];
}

export function formatGraphNodeContentDraftValue(
  field: GraphNodeContentFieldDefinition,
  payload: Record<string, unknown>,
): GraphNodeContentDraftValue {
  const value = readGraphNodeContentFieldValue(field, payload);

  switch (field.kind) {
    case "number":
      return typeof value === "number" && Number.isFinite(value) ? String(value) : "0";
    case "boolean":
      return typeof value === "boolean" ? value : false;
    case "select": {
      const selectedValue = typeof value === "string" ? value : field.options[0]?.value ?? "";
      const matchedOption = field.options.find((option) => option.value === selectedValue);
      return matchedOption?.value ?? field.options[0]?.value ?? "";
    }
    case "readonly":
    case "text":
    default:
      return typeof value === "string" ? value : "";
  }
}

export function parseGraphNodeContentDraftValue(
  field: GraphNodeContentFieldDefinition,
  draftValue: GraphNodeContentDraftValue,
): { ok: true; value: unknown } | { ok: false } {
  switch (field.kind) {
    case "number": {
      if (typeof draftValue !== "string") {
        return { ok: false };
      }

      const value = Number(draftValue);
      if (!Number.isFinite(value)) {
        return { ok: false };
      }

      return { ok: true, value };
    }
    case "boolean": {
      if (typeof draftValue !== "boolean") {
        return { ok: false };
      }

      return { ok: true, value: draftValue };
    }
    case "select": {
      if (typeof draftValue !== "string") {
        return { ok: false };
      }

      const matchedOption = field.options.find((option) => option.value === draftValue);
      if (!matchedOption) {
        return { ok: false };
      }

      return { ok: true, value: matchedOption.value };
    }
    case "readonly":
      return { ok: false };
    case "text":
    default:
      if (typeof draftValue !== "string") {
        return { ok: false };
      }

      return { ok: true, value: draftValue };
  }
}

export function buildGraphNodeContentPatch(
  field: GraphNodeContentFieldDefinition,
  draftValue: GraphNodeContentDraftValue,
): Record<string, unknown> | null {
  const parsed = parseGraphNodeContentDraftValue(field, draftValue);
  if (!parsed.ok) {
    return null;
  }

  return {
    [field.key]: parsed.value,
  };
}

export function projectGraphNodeContent(
  definition: GraphNodeContentDefinition,
  payload: Record<string, unknown>,
): GraphNodeContentProjection {
  return definition.buildProjection(payload);
}
