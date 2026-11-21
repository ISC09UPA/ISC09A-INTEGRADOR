import { tokens } from '../src/shared/theme/tokens';

describe('Design tokens', () => {
  it('uses the approved C08 visual system', () => {
    expect(tokens.colors.background).toBe('#F8F6EF');
    expect(tokens.colors.primaryText).toBe('#142E40');
    expect(tokens.colors.action).toBe('#087F78');
  });
});
