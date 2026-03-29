import type { PropsWithChildren } from "react";

interface PanelProps extends PropsWithChildren {
  title: string;
  description: string;
  className?: string;
  bodyClassName?: string;
}

function joinClassNames(...classNames: Array<string | undefined>) {
  return classNames.filter(Boolean).join(" ");
}

export function Panel(props: PanelProps) {
  const { title, description, children, className, bodyClassName } = props;

  return (
    <section className={joinClassNames("sb-panel", className)}>
      <header className="sb-panel-header">
        <h2>{title}</h2>
        <p>{description}</p>
      </header>
      <div className={joinClassNames("sb-panel-body", bodyClassName)}>{children}</div>
    </section>
  );
}
