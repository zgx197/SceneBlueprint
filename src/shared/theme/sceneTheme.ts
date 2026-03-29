export const sceneTheme = {
  background: "#d9d6cf",
  fogNear: 18,
  fogFar: 42,
  ambientIntensity: 1.25,
  directionalLight: {
    position: [9, 12, 7] as [number, number, number],
    intensity: 2.1,
  },
  hemisphere: {
    sky: "#f8fbff",
    ground: "#bcc3cc",
    intensity: 0.7,
  },
  grid: {
    major: "#a7b0ba",
    minor: "#c9cfd6",
  },
  ground: "#dedbd4",
  blocks: {
    primary: "#cfc9bf",
    secondary: "#d8d2c8",
  },
  markers: [
    { id: "entry", position: [-2.8, 0.45, 1.4] as [number, number, number], color: "#51a7d9" },
    { id: "focus", position: [0, 0.65, 0] as [number, number, number], color: "#df9b49" },
    { id: "exit", position: [2.6, 0.45, -1.8] as [number, number, number], color: "#61ae7d" },
  ],
};
