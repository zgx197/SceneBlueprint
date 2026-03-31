import { invokeHost } from "../bridge/tauriBridge";
import type {
  AppInfo,
  PingResult,
  ReadWorkspaceGraphFileRequest,
  ReadWorkspaceGraphFileResult,
  WorkspaceGraphFileInfo,
  WriteWorkspaceGraphFileRequest,
  WriteWorkspaceGraphFileResult,
  WriteWorkspaceRuntimeContractFileRequest,
  WriteWorkspaceRuntimeContractFileResult,
} from "../types/host";

export function readAppInfo(): Promise<AppInfo> {
  return invokeHost<AppInfo>("get_app_info");
}

export function pingHost(): Promise<PingResult> {
  return invokeHost<PingResult>("ping_host");
}

export function readLogFilePath(): Promise<string> {
  return invokeHost<string>("get_log_file_path");
}

export function readWorkspaceGraphFileInfo(): Promise<WorkspaceGraphFileInfo> {
  return invokeHost<WorkspaceGraphFileInfo>("get_workspace_graph_file_info");
}

export function readWorkspaceGraphFile(
  request: ReadWorkspaceGraphFileRequest = {},
): Promise<ReadWorkspaceGraphFileResult> {
  return invokeHost<ReadWorkspaceGraphFileResult>("read_workspace_graph_file", { request });
}

export function writeWorkspaceGraphFile(
  request: WriteWorkspaceGraphFileRequest,
): Promise<WriteWorkspaceGraphFileResult> {
  return invokeHost<WriteWorkspaceGraphFileResult>("write_workspace_graph_file", { request });
}

export function writeWorkspaceRuntimeContractFile(
  request: WriteWorkspaceRuntimeContractFileRequest,
): Promise<WriteWorkspaceRuntimeContractFileResult> {
  return invokeHost<WriteWorkspaceRuntimeContractFileResult>("write_workspace_runtime_contract_file", { request });
}
