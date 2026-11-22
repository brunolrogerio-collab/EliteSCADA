import React from 'react';

export function WorkflowFormSection({
  title,
  description,
  children,
  testId
}: {
  title: string;
  description?: string;
  children: React.ReactNode;
  testId?: string;
}) {
  return (
    <section className="eng-workflow-section" data-testid={testId}>
      <header>
        <strong>{title}</strong>
        {description && <span>{description}</span>}
      </header>
      {children}
    </section>
  );
}

export function WorkflowFormDisclosure({
  title,
  description,
  children,
  testId
}: {
  title: string;
  description?: string;
  children: React.ReactNode;
  testId?: string;
}) {
  return (
    <details className="eng-workflow-disclosure" data-testid={testId}>
      <summary>
        <span>
          <strong>{title}</strong>
          {description && <small>{description}</small>}
        </span>
        <b aria-hidden="true">+</b>
      </summary>
      <div className="eng-workflow-disclosure__body">{children}</div>
    </details>
  );
}
