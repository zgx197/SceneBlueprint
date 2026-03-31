export interface AppInfo {
  name: string;
  version: string;
  runtime: string;
  platform: string;
}

export interface PingResult {
  message: string;
  timestamp: string;
}

export interface WorkspaceGraphFileInfo {
  path: string;
  exists: boolean;
  backend: string;
}

export interface ReadWorkspaceGraphFileRequest {
  targetPath?: string;
}

export interface ReadWorkspaceGraphFileResult extends WorkspaceGraphFileInfo {
  content: string | null;
  readAt: string;
}

export interface WriteWorkspaceGraphFileRequest {
  content: string;
  targetPath?: string;
}

export interface WriteWorkspaceGraphFileResult extends WorkspaceGraphFileInfo {
  writtenAt: string;
}

export interface WorkspaceRuntimeContractFileInfo {
  path: string;
  exists: boolean;
  backend: string;
}

export interface WriteWorkspaceRuntimeContractFileRequest {
  content: string;
  targetPath?: string;
}

export interface WriteWorkspaceRuntimeContractFileResult extends WorkspaceRuntimeContractFileInfo {
  writtenAt: string;
}
