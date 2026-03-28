import { invokeHost } from "../bridge/tauriBridge";
import type { AppInfo, PingResult } from "../types/host";

export function readAppInfo(): Promise<AppInfo> {
  return invokeHost<AppInfo>("get_app_info");
}

export function pingHost(): Promise<PingResult> {
  return invokeHost<PingResult>("ping_host");
}
