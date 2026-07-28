import { MapPinned, ShieldCheck, Sparkles, Truck } from "lucide-react";

const benefits = [
  {
    icon: Truck,
    title: "Entrega para todo o Brasil",
    text: "Consulte as opções, valores e prazos disponíveis para o seu CEP.",
  },
  {
    icon: ShieldCheck,
    title: "Pagamento seguro",
    text: "Compre com segurança e acompanhe seu pedido pela sua conta.",
  },
  {
    icon: Sparkles,
    title: "Produtos selecionados",
    text: "Tarôs e itens esotéricos escolhidos com cuidado.",
  },
  {
    icon: MapPinned,
    title: "Entrega rápida em São Paulo",
    text: "Entrega no mesmo dia para regiões e horários elegíveis.",
  },
];

export function BenefitsSection() {
  return (
    <section className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      <h2 className="font-serif text-2xl text-esotera-secondary sm:text-3xl">
        Benefícios
      </h2>
      <p className="mt-2 text-sm text-esotera-muted">
        Por que comprar na Esotera.
      </p>
      <ul className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {benefits.map((item) => (
          <li
            key={item.title}
            className="rounded-lg border border-esotera-border bg-esotera-surface p-4 shadow-sm"
          >
            <item.icon className="text-esotera-primary" size={22} aria-hidden />
            <h3 className="mt-3 font-serif text-lg text-esotera-secondary">
              {item.title}
            </h3>
            <p className="mt-2 text-sm text-esotera-muted">{item.text}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
