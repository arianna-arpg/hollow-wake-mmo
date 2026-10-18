/** Click only after a fresh observation confirms the approached, hovered entity is still near. */
export async function approachAndClick({ within, timeoutMs, approach, aim, observe, click, wait, now = Date.now }) {
  const deadline = now() + timeoutMs;
  while (now() < deadline) {
    if (!await approach(deadline - now())) return false;
    await aim();
    const { self, target, hovered, screen } = await observe();
    if (!self || !target || self.dead || now() >= deadline) return false;
    if (Math.hypot(self.x - target.x, self.y - target.y) <= within
        && String(hovered) === String(target.key) && screen?.visible) {
      await click(screen);
      return true;
    }
    // The target may have walked away while aiming. Continue walking, without sending a stale interaction.
    await wait(100);
  }
  return false;
}
