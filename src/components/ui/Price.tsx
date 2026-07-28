import { formatCurrency } from "@/utils/format";

type PriceProps = {
  value: number;
  className?: string;
};

export function Price({ value, className = "" }: PriceProps) {
  return (
    <span
      className={`font-semibold tabular-nums text-esotera-primary ${className}`}
    >
      {formatCurrency(value)}
    </span>
  );
}
