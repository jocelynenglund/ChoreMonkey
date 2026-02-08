import { Toaster } from "@/components/ui/toaster";
import { Toaster as Sonner } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import Index from "./pages/Index";
import CreateHousehold from "./pages/CreateHousehold";
import JoinHousehold from "./pages/JoinHousehold";
import AccessHousehold from "./pages/AccessHousehold";
import HouseholdDashboard from "./pages/HouseholdDashboard";
import NotFound from "./pages/NotFound";

const queryClient = new QueryClient();

const App = () => (
  <QueryClientProvider client={queryClient}>
    <BrowserRouter>
      <TooltipProvider>
        <Toaster />
        <Sonner />
        <Routes>
          <Route path="/" element={<Index />} />
          <Route path="/create" element={<CreateHousehold />} />
          <Route path="/join" element={<JoinHousehold />} />
          <Route path="/join/:inviteCode" element={<JoinHousehold />} />
          <Route path="/access/:id" element={<AccessHousehold />} />
          <Route path="/household/:id" element={<HouseholdDashboard />} />
          {/* ADD ALL CUSTOM ROUTES ABOVE THE CATCH-ALL "*" ROUTE */}
          <Route path="*" element={<NotFound />} />
        </Routes>
      </TooltipProvider>
    </BrowserRouter>
  </QueryClientProvider>
);

export default App;
