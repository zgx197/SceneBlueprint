import { afterEach, describe, expect, it, vi } from "vitest";
import { createGraphDocument } from "../document/graphDocument";
import { createReducerGraphCommandBus } from "./graphCommands";
import { createTestRuntimeState, instantiateTestNode } from "../testing/graphTestUtils";

describe("graphCommands", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("supports undo and redo for reducer-backed state changes", () => {
    const node = instantiateTestNode("flow.start", "node-start", { x: 0, y: 0 });
    const bus = createReducerGraphCommandBus({
      initialState: createTestRuntimeState(createGraphDocument({ id: "graph", nodes: [node] })),
      reduce(state, command) {
        switch (command.type) {
          case "graph.move-nodes":
            return {
              ...state,
              document: {
                ...state.document,
                nodes: state.document.nodes.map((entry) =>
                  command.nodeIds.includes(entry.id)
                    ? {
                        ...entry,
                        position: {
                          x: entry.position.x + command.delta.x,
                          y: entry.position.y + command.delta.y,
                        },
                      }
                    : entry),
              },
            };
          default:
            return state;
        }
      },
    });

    bus.execute({
      type: "graph.move-nodes",
      nodeIds: ["node-start"],
      delta: { x: 12, y: -4 },
    });

    expect(bus.getState().document.nodes[0]?.position).toEqual({ x: 12, y: -4 });
    expect(bus.undo()?.document.nodes[0]?.position).toEqual({ x: 0, y: 0 });
    expect(bus.redo()?.document.nodes[0]?.position).toEqual({ x: 12, y: -4 });
  });

  it("merges repeat move commands inside the merge window", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-03-30T00:00:00.000Z"));

    const node = instantiateTestNode("flow.start", "node-start", { x: 0, y: 0 });
    const bus = createReducerGraphCommandBus({
      initialState: createTestRuntimeState(createGraphDocument({ id: "graph", nodes: [node] })),
      reduce(state, command) {
        switch (command.type) {
          case "graph.move-nodes":
            return {
              ...state,
              document: {
                ...state.document,
                nodes: state.document.nodes.map((entry) =>
                  command.nodeIds.includes(entry.id)
                    ? {
                        ...entry,
                        position: {
                          x: entry.position.x + command.delta.x,
                          y: entry.position.y + command.delta.y,
                        },
                      }
                    : entry),
              },
            };
          default:
            return state;
        }
      },
    });

    bus.execute({
      type: "graph.move-nodes",
      nodeIds: ["node-start"],
      delta: { x: 10, y: 0 },
    });
    vi.setSystemTime(new Date("2026-03-30T00:00:00.800Z"));
    bus.execute({
      type: "graph.move-nodes",
      nodeIds: ["node-start"],
      delta: { x: 5, y: 0 },
    });

    const snapshot = bus.getSnapshot();

    expect(snapshot.historyLength).toBe(1);
    expect(snapshot.history[0]?.commandCount).toBe(2);
    expect(bus.getState().document.nodes[0]?.position).toEqual({ x: 15, y: 0 });
    expect(bus.undo()?.document.nodes[0]?.position).toEqual({ x: 0, y: 0 });
  });

  it("clears redo history after executing a new command on a rewound state", () => {
    const node = instantiateTestNode("flow.start", "node-start", { x: 0, y: 0 });
    const bus = createReducerGraphCommandBus({
      initialState: createTestRuntimeState(createGraphDocument({ id: "graph", nodes: [node] })),
      reduce(state, command) {
        switch (command.type) {
          case "graph.move-nodes":
            return {
              ...state,
              document: {
                ...state.document,
                nodes: state.document.nodes.map((entry) =>
                  command.nodeIds.includes(entry.id)
                    ? {
                        ...entry,
                        position: {
                          x: entry.position.x + command.delta.x,
                          y: entry.position.y + command.delta.y,
                        },
                      }
                    : entry),
              },
            };
          default:
            return state;
        }
      },
    });

    bus.execute({
      type: "graph.move-nodes",
      nodeIds: ["node-start"],
      delta: { x: 10, y: 0 },
    });
    bus.undo();

    expect(bus.getSnapshot().canRedo).toBe(true);

    bus.execute({
      type: "graph.move-nodes",
      nodeIds: ["node-start"],
      delta: { x: 3, y: 2 },
    });

    const snapshot = bus.getSnapshot();
    expect(snapshot.canRedo).toBe(false);
    expect(snapshot.redoLength).toBe(0);
    expect(bus.getState().document.nodes[0]?.position).toEqual({ x: 3, y: 2 });
  });
});
