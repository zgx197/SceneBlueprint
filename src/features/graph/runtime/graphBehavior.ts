import type { GraphTopologyPolicy } from "../profile/graphProfile";
import {
  createGraphConnectionPolicy,
  type CreateGraphConnectionPolicyOptions,
  type GraphConnectionPolicy,
  type GraphConnectionValidator,
} from "./graphConnectionPolicy";
import {
  createGraphTypeCompatibilityRegistry,
  type GraphTypeCompatibilityRegistry,
} from "./graphTypeCompatibility";

export interface GraphBehavior {
  readonly topologyPolicy: GraphTopologyPolicy;
  readonly typeCompatibility: GraphTypeCompatibilityRegistry;
  readonly validators: readonly GraphConnectionValidator[];
  readonly connectionPolicy: GraphConnectionPolicy;
}

export interface CreateGraphBehaviorOptions {
  topologyPolicy?: GraphTopologyPolicy;
  typeCompatibility?: GraphTypeCompatibilityRegistry;
  validators?: GraphConnectionValidator[];
  connectionPolicy?: GraphConnectionPolicy;
}

export function createGraphBehavior(options: CreateGraphBehaviorOptions = {}): GraphBehavior {
  const topologyPolicy = options.topologyPolicy ?? "dag";
  const typeCompatibility = options.typeCompatibility ?? createGraphTypeCompatibilityRegistry();
  const validators = options.validators ?? [];
  const connectionPolicyOptions: CreateGraphConnectionPolicyOptions = {
    topologyPolicy,
    typeCompatibility,
    validators,
  };

  return {
    topologyPolicy,
    typeCompatibility,
    validators: [...validators],
    connectionPolicy: options.connectionPolicy ?? createGraphConnectionPolicy(connectionPolicyOptions),
  };
}
