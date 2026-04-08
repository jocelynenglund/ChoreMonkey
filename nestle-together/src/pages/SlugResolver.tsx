import { useEffect, useState } from 'react';
import { useParams, Navigate } from 'react-router-dom';
import { getHouseholdBySlug } from '@/features/household/api';

export default function SlugResolver() {
  const { slug } = useParams<{ slug: string }>();
  const [householdId, setHouseholdId] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    if (!slug) return;
    getHouseholdBySlug(slug).then((result) => {
      if (result) {
        setHouseholdId(result.householdId);
      } else {
        setNotFound(true);
      }
    });
  }, [slug]);

  if (notFound) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="w-12 h-12 rounded-xl gradient-monkey flex items-center justify-center mx-auto mb-4">
            <span className="text-2xl">🐵</span>
          </div>
          <h1 className="text-xl font-bold mb-2">Household not found</h1>
          <p className="text-muted-foreground">
            No household exists at <span className="font-mono">/h/{slug}</span>
          </p>
        </div>
      </div>
    );
  }

  if (householdId) {
    return <Navigate to={`/access/${householdId}`} replace />;
  }

  return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="text-center">
        <div className="w-12 h-12 rounded-xl gradient-monkey flex items-center justify-center mx-auto mb-4 animate-pulse">
          <span className="text-2xl">🐵</span>
        </div>
        <p className="text-muted-foreground">Looking up household...</p>
      </div>
    </div>
  );
}
