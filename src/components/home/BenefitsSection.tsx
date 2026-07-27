import { MapPinned, ShieldCheck, Sparkles, Truck } from "lucide-react";

const benefits = [
  {
    icon: Truck,
    title: "Entrega para todo o Brasil",
    text: "Envios simulados com modalidades Econômico e Expresso.",
  },
  {
    icon: ShieldCheck,
    title: "Pagamento seguro",
    text: "Fluxo de pagamento simulado — sem cobrança real.",
  },
  {
    icon: Sparkles,
    title: "Produtos selecionados",
    text: "Tarôs e itens esotéricos escolhidos com cuidado.",
  },
  {
    icon: MapPinned,
    title: "Mesmo dia em SP",
    text: "Entrega no mesmo dia para regiões elegíveis de São Paulo (J3 simulada).",
  },
];

export function BenefitsSection() {
  return (
    <section className="mx-auto max-w-6xl px-4 py-16 sm:px-6">
      <h2 className="font-serif text-3xl text-esotera-white">Benefícios</h2>
      <p className="mt-2 text-sm text-esotera-muted">
        O que você encontra nesta experiência de compra.
      </p>
      <ul className="mt-10 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {benefits.map((item) => (
          <li key={item.title} className="rounded-lg border border-esotera-graphite/70 p-5">
            <item.icon className="text-esotera-gold" size={22} aria-hidden />
            <h3 className="mt-4 font-serif text-lg text-esotera-beige">
              {item.title}
            </h3>
            <p className="mt-2 text-sm text-esotera-muted">{item.text}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
