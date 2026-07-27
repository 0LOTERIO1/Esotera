import { shippingOrigin } from "@/config/shipping";

export function ShippingInfoSection() {
  return (
    <section className="border-y border-esotera-graphite/60 bg-esotera-purple/20 py-14">
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <h2 className="font-serif text-3xl text-esotera-white">Entrega</h2>
        <p className="mt-3 max-w-2xl text-sm text-esotera-muted">
          Enviamos a partir de {shippingOrigin.region}, {shippingOrigin.city}{" "}
          (CEP {shippingOrigin.cep}). Embalagem de referência:{" "}
          {shippingOrigin.package.widthCm} × {shippingOrigin.package.heightCm} ×{" "}
          {shippingOrigin.package.lengthCm} cm · {shippingOrigin.package.weightGrams} g.
        </p>
        <p className="mt-3 max-w-2xl text-sm text-esotera-muted">
          Frete grátis a partir de R$ 99,90 (após desconto) para Sul e Sudeste.
          Modalidade J3 simulada para CEPs elegíveis em São Paulo.
        </p>
      </div>
    </section>
  );
}
