import type { OrderStatus } from "@/types";
import { orderStatusLabels } from "@/utils/labels";

const styles: Record<OrderStatus, string> = {
  awaiting_payment: "border-amber-500/40 text-amber-200 bg-amber-500/10",
  payment_approved: "border-esotera-success/40 text-emerald-200 bg-esotera-success/10",
  preparing: "border-sky-500/40 text-sky-200 bg-sky-500/10",
  shipped: "border-esotera-gold/40 text-esotera-gold-soft bg-esotera-gold/10",
  delivered: "border-esotera-beige/30 text-esotera-beige bg-esotera-beige/10",
  cancelled: "border-esotera-error/40 text-red-200 bg-esotera-error/10",
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
