import { HeroBanner } from "@/components/home/HeroBanner";
import { FeaturedProduct } from "@/components/home/FeaturedProduct";
import { SelectedProducts } from "@/components/home/SelectedProducts";
import { BenefitsSection } from "@/components/home/BenefitsSection";
import { ShippingInfoSection } from "@/components/home/ShippingInfoSection";
import { NewsletterSection } from "@/components/home/NewsletterSection";

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
