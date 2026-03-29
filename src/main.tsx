import ReactDOM from "react-dom/client";
import App from "./App";
import { AppProviders } from "./app/providers/AppProviders";
import "./shared/styles/tokens.css";
import "./shared/styles/globals.css";

const root = ReactDOM.createRoot(document.getElementById("root")!);

root.render(
  <AppProviders>
    <App />
  </AppProviders>,
);

window.requestAnimationFrame(() => {
  document.getElementById("boot-splash")?.remove();
});
