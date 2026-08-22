import { NumberFractionPipe } from './number-fraction.pipe';

describe('NumberFractionPipe', () => {
  it('create an instance', () => {
    const pipe = new NumberFractionPipe();
    expect(pipe).toBeTruthy();
  });
});
