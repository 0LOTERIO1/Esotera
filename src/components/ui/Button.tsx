import Link from "next/link";

type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";

const variants: Record<ButtonVariant, string> = {
  primary:
    "bg-esotera-gold text-esotera-black hover:bg-esotera-gold-soft disabled:opacity-50",
  secondary:
    "border border-esotera-gold/50 text-esotera-gold hover:border-esotera-gold hover:bg-esotera-gold/10 disabled:opacity-50",
  ghost:
    "text-esotera-beige hover:text-esotera-gold disabled:opacity-50",
  danger:
    "border border-esotera-error/50 text-red-200 hover:bg-esotera-error/10 disabled:opacity-50",
};

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
      className={`inline-flex min-h-11 items-center justify-center rounded-md px-4 py-2.5 text-sm font-medium transition ${variants[variant]} ${className}`}
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
      className={`inline-flex min-h-11 items-center justify-center rounded-md px-4 py-2.5 text-sm font-medium transition ${variants[variant]} ${className}`}
    >
      {children}
    </Link>
  );
}
