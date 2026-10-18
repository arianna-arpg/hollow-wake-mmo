import { it, expect } from 'vitest';
// The audit deliberately also runs standalone without TypeScript or a second package installation.
// @ts-expect-error repository-local JavaScript experiment has no declaration file
import { audit, loadAudit } from '../../../experiments/visual-convergence/audit.mjs';
import { castPrimitiveKinds } from './castVisualPrimitives';

it('classifies every captured prototype mechanic and keeps both primitive registries aligned', () => {
  const data = loadAudit();
  expect(audit(data).errors).toEqual([]);
  expect(castPrimitiveKinds()).toEqual(data.coverage.primitives);
});
it('rejects unclassified future skill, support and nested variant vocabulary', () => {
  const data = loadAudit();
  data.skills[0].fields.push('newMechanic');
  data.supports[0].fields.push('newSupportMechanic');
  data.skills[0].variants.push({ path: 'tree.nodes.0.newShape.delivery', kind: 'cone' });
  expect(audit(data).errors).toHaveLength(3);
});
