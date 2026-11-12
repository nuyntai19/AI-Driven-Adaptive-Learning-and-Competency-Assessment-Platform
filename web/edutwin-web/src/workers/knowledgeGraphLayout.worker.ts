import { createLayoutWorkerState, handleLayoutRequest, type LayoutRequest, type LayoutResponse } from "../utils/knowledgeGraphLayoutProtocol";

/** Knowledge-graph layout, routing and label placement off the main thread. */
const state = createLayoutWorkerState();
// Typed minimally: the app tsconfig uses the DOM lib, whose `self` is a Window.
const scope = self as unknown as {
  onmessage: ((event: MessageEvent<LayoutRequest>) => void) | null;
  postMessage(message: LayoutResponse): void;
};

scope.onmessage = (event) => {
  scope.postMessage(handleLayoutRequest(state, event.data));
};
