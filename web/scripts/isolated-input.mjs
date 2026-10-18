// Runs before application scripts in each test page. Never changes the operator's devices.
export function isolatePhysicalInput() {
  Object.defineProperty(navigator, 'getGamepads', { configurable: true, value: () => [] });
  for (const type of ['gamepadconnected', 'gamepaddisconnected'])
    addEventListener(type, event => event.stopImmediatePropagation(), true);
}
