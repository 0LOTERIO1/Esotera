type FormFieldProps = {
  label: string;
  id: string;
  error?: string;
  hint?: string;
  required?: boolean;
  children: React.ReactNode;
};

export function FormField({
  label,
  id,
  error,
  hint,
  required,
  children,
}: FormFieldProps) {
  const errorId = error ? `${id}-error` : undefined;
  const hintId = hint ? `${id}-hint` : undefined;

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm text-esotera-beige">
        {label}
        {required ? (
          <span className="text-esotera-gold" aria-hidden>
            {" "}
            *
          </span>
        ) : null}
      </label>
      {children}
      {hint && !error ? (
        <p id={hintId} className="text-xs text-esotera-muted">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} role="alert" className="text-xs text-esotera-error">
          {error}
        </p>
      ) : null}
    </div>
  );
}

export const inputClassName =
  "w-full rounded-md border border-esotera-graphite bg-esotera-black/50 px-3 py-2.5 text-sm text-esotera-white placeholder:text-esotera-muted/70 transition focus:border-esotera-gold";
