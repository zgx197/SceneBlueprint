import type { PropsWithChildren } from "react";

interface PanelProps extends PropsWithChildren {
  title: string;
  description: string;
}

export function Panel(props: PanelProps) {
  const { title, description, children } = props;

  return (
    <section className="sb-panel">
      <header className="sb-panel-header">
        <h2>{title}</h2>
        <p>{description}</p>
      </header>
      <div className="sb-panel-body">{children}</div>
    </section>
  );
}
