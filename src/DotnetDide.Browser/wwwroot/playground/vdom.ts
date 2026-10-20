export { h, init, attributesModule, propsModule, eventListenersModule, classModule, styleModule } from "../vendor/snabbdom/build/index.js";
export type { VNode } from "../vendor/snabbdom/build/vnode.js";

export function download(data: BlobPart, name: string, type = "application/json"): void {
  const url = URL.createObjectURL(new Blob([data], {type}));
  const anchor = document.createElement("a");
  anchor.href = url; anchor.download = name; anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
export function fileSize(bytes: number): string {
  return bytes < 1024 ? `${bytes} B` : bytes < 1048576 ? `${(bytes / 1024).toFixed(1)} KiB` : `${(bytes / 1048576).toFixed(1)} MiB`;
}
