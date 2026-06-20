import { ErrMsgPipe } from './err-msg.pipe';

describe('ErrMsgPipe', () => {
  it('create an instance', () => {
    const pipe = new ErrMsgPipe();
    expect(pipe).toBeTruthy();
  });
});
