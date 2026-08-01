import type { Metadata } from "next";
import { HeroBanner } from "@/components/home/HeroBanner";
import { FeaturedProduct } from "@/components/home/FeaturedProduct";
import { SelectedProducts } from "@/components/home/SelectedProducts";
import { BenefitsSection } from "@/components/home/BenefitsSection";
import { ShippingInfoSection } from "@/components/home/ShippingInfoSection";
import { NewsletterSection } from "@/components/home/NewsletterSection";
import { storeConfig } from "@/config/store";

export const metadata: Metadata = {
  title: {
    absolute: `${storeConfig.name} | Tarôs e produtos esotéricos`,
  },
  description: storeConfig.description,
  alternates: { canonical: "/" },
};

export default function HomePage() {
  return (
    <>
      <HeroBanner />
      <FeaturedProduct />
      <SelectedProducts />
      <BenefitsSection />
      <ShippingInfoSection />
      <NewsletterSection />
    </>
  );
}
