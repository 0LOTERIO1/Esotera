type EmptyStateProps = {
  title: string;
  description?: string;
  action?: React.ReactNode;
};

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="rounded-lg border border-dashed border-esotera-border bg-esotera-surface px-6 py-12 text-center">
      <h2 className="font-serif text-xl text-esotera-secondary">{title}</h2>
      {description ? (
        <p className="mx-auto mt-2 max-w-md text-sm text-esotera-muted">
          {description}
        </p>
      ) : null}
      {action ? <div className="mt-6">{action}</div> : null}
    </div>
  );
}
