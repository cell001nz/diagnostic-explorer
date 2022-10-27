export function recursiveFlatMap<T>(arr: T[], iter: (t: T) => T[]): T[] {
  const result: T[] = [];

  function recurse(next: T[]): void {
    for (const item of next) {
      result.push(item);
      const children = iter(item);
      if (children != null && children.length > 0)
        recurse(children);
    }
  }

  recurse(arr);

  return result;
}
