import type { OrderStatus } from "@/types";
import { orderStatusLabels } from "@/utils/labels";

const styles: Record<OrderStatus, string> = {
  awaiting_payment: "border-amber-500/40 text-amber-800 bg-amber-50",
  payment_approved: "border-esotera-success/40 text-esotera-success bg-esotera-success/10",
  preparing: "border-sky-500/40 text-sky-800 bg-sky-50",
  shipped: "border-esotera-primary/40 text-esotera-primary bg-esotera-primary/10",
  delivered: "border-esotera-border text-esotera-secondary bg-esotera-surface-secondary",
  cancelled: "border-esotera-error/40 text-esotera-error bg-esotera-error/10",
};

export function StatusBadge({ status }: { status: OrderStatus }) {
  return (
    <span
      className={`inline-flex rounded border px-2 py-0.5 text-xs ${styles[status]}`}
    >
      {orderStatusLabels[status]}
    </span>
  );
}
