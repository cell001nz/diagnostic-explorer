export class PromptResult
{
  constructor(readonly button: "OK" | "Cancel", readonly value: string) {
  }
}

export class PromptData {

  constructor(readonly text: string, readonly value: string) {
    this.value = value;
  }
}
