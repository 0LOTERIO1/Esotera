import { formatCurrency } from "@/utils/format";

type PriceProps = {
  value: number;
  className?: string;
};

export function Price({ value, className = "" }: PriceProps) {
  return (
    <span className={`font-medium tabular-nums text-esotera-gold ${className}`}>
      {formatCurrency(value)}
    </span>
  );
}
