export function LoadingState({ label = "Carregando…" }: { label?: string }) {
  return (
    <div className="space-y-3" role="status" aria-live="polite">
      <span className="sr-only">{label}</span>
      <div className="skeleton h-8 w-1/3 rounded" />
      <div className="skeleton h-40 w-full rounded" />
      <div className="skeleton h-24 w-full rounded" />
    </div>
  );
}
