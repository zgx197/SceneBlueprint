import React, { type ErrorInfo, type ReactNode } from "react";
import { writeAppLog } from "../logging/LogContext";

interface RuntimeErrorBoundaryProps {
  scope: string;
  fallback: ReactNode;
  children: ReactNode;
}

interface RuntimeErrorBoundaryState {
  hasError: boolean;
}

function describeError(error: unknown) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  if (typeof error === "string" && error.length > 0) {
    return error;
  }

  return "发生未知错误";
}

export class RuntimeErrorBoundary extends React.Component<
  RuntimeErrorBoundaryProps,
  RuntimeErrorBoundaryState
> {
  public state: RuntimeErrorBoundaryState = {
    hasError: false,
  };

  public static getDerivedStateFromError(): RuntimeErrorBoundaryState {
    return {
      hasError: true,
    };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    const message = describeError(error);
    const componentStack = errorInfo.componentStack?.replace(/\s+/g, " ").trim();
    const details = componentStack ? `${message} | ${componentStack}` : message;
    writeAppLog("error", this.props.scope, details);
  }

  public render() {
    if (this.state.hasError) {
      return this.props.fallback;
    }

    return this.props.children;
  }
}
