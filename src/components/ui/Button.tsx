import Link from "next/link";

type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";

const variants: Record<ButtonVariant, string> = {
  primary:
    "bg-esotera-primary text-white hover:bg-esotera-primary-hover focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-esotera-primary disabled:opacity-50",
  secondary:
    "border border-esotera-primary bg-esotera-surface text-esotera-primary hover:bg-esotera-surface-secondary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-esotera-primary disabled:opacity-50",
  ghost:
    "text-esotera-secondary hover:bg-esotera-surface-secondary hover:text-esotera-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-esotera-primary disabled:opacity-50",
  danger:
    "border border-esotera-error/40 text-esotera-error hover:bg-esotera-error/10 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-esotera-error disabled:opacity-50",
};

/** Garante texto branco no primário (contraste sobre fundo azul) */
const primaryTextFix = "!text-white hover:!text-white";

type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
};

export function Button({
  variant = "primary",
  className = "",
  children,
  ...props
}: ButtonProps) {
  return (
    <button
      className={`inline-flex min-h-11 items-center justify-center rounded-md px-4 py-2.5 text-sm font-medium transition ${variants[variant]} ${
        variant === "primary" ? primaryTextFix : ""
      } ${className}`}
      {...props}
    >
      {children}
    </button>
  );
}

export function ButtonLink({
  href,
  variant = "primary",
  className = "",
  children,
}: {
  href: string;
  variant?: ButtonVariant;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      className={`inline-flex min-h-11 items-center justify-center rounded-md px-4 py-2.5 text-sm font-medium transition ${variants[variant]} ${
        variant === "primary" ? primaryTextFix : ""
      } ${className}`}
    >
      {children}
    </Link>
  );
}
