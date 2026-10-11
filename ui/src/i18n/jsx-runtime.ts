import { jsx as reactJsx, jsxs as reactJsxs, Fragment } from 'react/jsx-runtime';
import type { JSX as ReactJSX } from 'react';
import { translate } from './translate';
export { Fragment };
export namespace JSX {
  export type ElementType = ReactJSX.ElementType;
  export type Element = ReactJSX.Element;
  export type IntrinsicElements = ReactJSX.IntrinsicElements;
  export type ElementChildrenAttribute = ReactJSX.ElementChildrenAttribute;
  export type IntrinsicAttributes = ReactJSX.IntrinsicAttributes;
  export type LibraryManagedAttributes<C, P> = ReactJSX.LibraryManagedAttributes<C, P>;
}
// Translate text at element creation, without mutating React-managed DOM or data.
// Device names, paths and backend messages stay intact unless explicitly in the catalog.
function text(value: unknown): unknown {
  return typeof value === 'string' ? translate(value) : Array.isArray(value) ? value.map(text) : value;
}
function propsFor(props: unknown) {
  const next = {...(props as Record<string, unknown>)};
  for (const key of ['children','aria-label','title','placeholder']) if (key in next) next[key] = text(next[key]);
  return next;
}
export const jsx: typeof reactJsx = (type, props, key) => reactJsx(type, propsFor(props), key);
export const jsxs: typeof reactJsxs = (type, props, key) => reactJsxs(type, propsFor(props), key);
