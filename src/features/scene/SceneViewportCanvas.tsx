import { useMemo } from "react";
import { OrbitControls } from "@react-three/drei";
import { Canvas } from "@react-three/fiber";
import type { GraphRuntimeBridgeContract } from "../graph/runtime/graphWorkspaceBridge";
import type { GraphWorkspaceIssue } from "../graph/runtime/graphWorkspaceExport";
import { sceneTheme } from "../../shared/theme/sceneTheme";
import { createSceneViewportBridgeModel } from "./sceneViewportBridge";

interface SceneViewportCanvasProps {
  bridgeContract: GraphRuntimeBridgeContract;
  issues: GraphWorkspaceIssue[];
  selectedMarkerId: string | null;
  onSelectMarker: (markerId: string) => void;
  onClearSelection: () => void;
}

function MarkerBlock(props: {
  marker: ReturnType<typeof createSceneViewportBridgeModel>["markers"][number];
  onSelectMarker: (markerId: string) => void;
}) {
  const { marker, onSelectMarker } = props;
  const height = marker.selected ? 1.1 : 0.9;

  return (
    <group
      position={marker.position}
      onClick={(event) => {
        event.stopPropagation();
        onSelectMarker(marker.id);
      }}
    >
      <mesh castShadow receiveShadow scale={marker.selected ? 1.12 : 1}>
        <boxGeometry args={[0.42, height, 0.42]} />
        <meshStandardMaterial color={marker.color} roughness={0.48} metalness={0.02} />
      </mesh>
      <mesh position={[0, 0.62, 0]} scale={marker.selected ? 1.14 : 1}>
        <sphereGeometry args={[0.14, 24, 24]} />
        <meshStandardMaterial color={marker.color} emissive={marker.color} emissiveIntensity={0.18} />
      </mesh>
    </group>
  );
}

function SceneDraftStage(props: {
  model: ReturnType<typeof createSceneViewportBridgeModel>;
  onSelectMarker: (markerId: string) => void;
}) {
  const { model, onSelectMarker } = props;

  return (
    <>
      <color attach="background" args={[sceneTheme.background]} />
      <fog attach="fog" args={[sceneTheme.background, sceneTheme.fogNear, sceneTheme.fogFar]} />
      <ambientLight intensity={sceneTheme.ambientIntensity} />
      <directionalLight
        castShadow
        position={sceneTheme.directionalLight.position}
        intensity={sceneTheme.directionalLight.intensity}
        shadow-mapSize-width={1024}
        shadow-mapSize-height={1024}
      />
      <hemisphereLight
        args={[
          sceneTheme.hemisphere.sky,
          sceneTheme.hemisphere.ground,
          sceneTheme.hemisphere.intensity,
        ]}
      />

      <group>
        <gridHelper
          args={[24, 24, sceneTheme.grid.major, sceneTheme.grid.minor]}
          position={[0, 0.001, 0]}
        />

        <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -0.02, 0]} receiveShadow>
          <planeGeometry args={[24, 24]} />
          <meshStandardMaterial color={sceneTheme.ground} roughness={0.98} metalness={0.02} />
        </mesh>

        <mesh position={[0, 0.4, 0]} castShadow receiveShadow>
          <boxGeometry args={[1.8, 0.8, 1.8]} />
          <meshStandardMaterial color={sceneTheme.blocks.primary} roughness={0.88} metalness={0.02} />
        </mesh>

        <mesh position={[-3.8, 0.7, -2.6]} castShadow receiveShadow>
          <boxGeometry args={[1.2, 1.4, 1.2]} />
          <meshStandardMaterial color={sceneTheme.blocks.secondary} roughness={0.88} metalness={0.02} />
        </mesh>

        <mesh position={[4.1, 0.55, 2.8]} castShadow receiveShadow>
          <boxGeometry args={[1.4, 1.1, 1.4]} />
          <meshStandardMaterial color={sceneTheme.blocks.secondary} roughness={0.88} metalness={0.02} />
        </mesh>

        {model.markers.map((marker) => (
          <MarkerBlock key={marker.id} marker={marker} onSelectMarker={onSelectMarker} />
        ))}
      </group>

      <axesHelper args={[1.8]} position={[-5.5, 0.01, 5.5]} />
      <OrbitControls
        makeDefault
        enableDamping
        dampingFactor={0.08}
        minDistance={4}
        maxDistance={22}
        minPolarAngle={0.35}
        maxPolarAngle={Math.PI / 2.05}
        target={[0, 0.7, 0]}
      />
    </>
  );
}

export function SceneViewportCanvas(props: SceneViewportCanvasProps) {
  const { bridgeContract, issues, selectedMarkerId, onSelectMarker, onClearSelection } = props;
  const model = useMemo(() => {
    return createSceneViewportBridgeModel(bridgeContract, issues, selectedMarkerId);
  }, [bridgeContract, issues, selectedMarkerId]);
  const selectedMarker = model.markers.find((marker) => marker.id === selectedMarkerId) ?? null;

  return (
    <div className="sb-scene-viewport">
      <Canvas
        shadows
        dpr={[1, 1.75]}
        camera={{ position: [7.8, 6.2, 7.8], fov: 42 }}
        fallback={<div className="sb-scene-fallback">当前环境暂不支持 WebGL 视口。</div>}
        onPointerMissed={() => {
          onClearSelection();
        }}
      >
        <SceneDraftStage model={model} onSelectMarker={onSelectMarker} />
      </Canvas>

      <div className="sb-scene-overlay sb-scene-overlay-top">
        <span className="sb-scene-chip">Bridge Viewport</span>
        <span className="sb-scene-chip">Project {model.projectId ?? "<未指定>"}</span>
        <span className="sb-scene-chip">Scene {model.sceneId ?? "<未指定>"}</span>
        <span className="sb-scene-chip">Marker x {model.markerCount}</span>
        <span className="sb-scene-chip">Warn {model.warningCount}</span>
        <span className="sb-scene-chip">Error {model.errorCount}</span>
      </div>

      <div className="sb-scene-overlay sb-scene-overlay-bottom">
        <span>左键旋转</span>
        <span>右键平移</span>
        <span>滚轮缩放</span>
        <span>{selectedMarker ? `当前 Marker：${selectedMarker.label}` : "单击 Marker 进入 Inspector"}</span>
      </div>
    </div>
  );
}
