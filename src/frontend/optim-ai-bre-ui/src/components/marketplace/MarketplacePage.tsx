'use client'

import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/lib/api-client'

interface MarketplaceTemplate {
  id: string
  name: string
  description: string
  category: string
  loanProduct: string
  author: string
  rating: number
  downloads: number
  ruleCount: number
  tags: string[]
  isFeatured: boolean
  isPremium: boolean
  lastUpdated: string
}

const MOCK_TEMPLATES: MarketplaceTemplate[] = [
  { id: '1', name: 'Complete Vehicle Loan Decisioning Pack', description: 'Industry-standard rule set covering CIBIL checks, income verification, LTV calculations, and collateral assessment for vehicle loans.', category: 'Vehicle Finance', loanProduct: 'Vehicle Loan', author: 'OPTIM AI Team', rating: 4.8, downloads: 1240, ruleCount: 18, tags: ['CIBIL', 'LTV', 'Income', 'KYC'], isFeatured: true, isPremium: false, lastUpdated: '2024-06-01' },
  { id: '2', name: 'MSME Credit Assessment Framework', description: 'Comprehensive MSME underwriting with GST return analysis, ITR verification, banking turnover checks, and promoter assessment.', category: 'MSME', loanProduct: 'MSME Loan', author: 'FinTech Labs', rating: 4.6, downloads: 842, ruleCount: 24, tags: ['GST', 'ITR', 'Banking', 'MSME'], isFeatured: true, isPremium: true, lastUpdated: '2024-05-15' },
  { id: '3', name: 'Tractor & Agri Loan Rules', description: 'Specialized rule pack for agricultural equipment financing with crop income assessment and land holding verification.', category: 'Agri Finance', loanProduct: 'Tractor Loan', author: 'AgriFinance Experts', rating: 4.4, downloads: 520, ruleCount: 14, tags: ['Agriculture', 'Tractor', 'Crop Income'], isFeatured: false, isPremium: false, lastUpdated: '2024-04-20' },
  { id: '4', name: 'Personal Loan Quick Decisioning', description: 'Fast-track personal loan assessment for salaried individuals with employer checks, salary slip verification, and EMI calculation.', category: 'Personal Finance', loanProduct: 'Personal Loan', author: 'RapidLend Solutions', rating: 4.5, downloads: 1890, ruleCount: 12, tags: ['Salaried', 'Quick Decision', 'EMI'], isFeatured: false, isPremium: false, lastUpdated: '2024-06-10' },
  { id: '5', name: 'Commercial Vehicle Loan Pack', description: 'End-to-end CV loan decisioning covering route profitability, vehicle age checks, permit validity, and driver vintage.', category: 'Commercial Finance', loanProduct: 'CV Loan', author: 'OPTIM AI Team', rating: 4.7, downloads: 394, ruleCount: 20, tags: ['Commercial', 'Route', 'Permit', 'Fleet'], isFeatured: true, isPremium: true, lastUpdated: '2024-05-28' },
  { id: '6', name: 'Compliance & Regulatory Rules', description: 'RBI compliance rules including wilful defaulter check, PMLA screening, CERSAI verification, and SFMS compliance.', category: 'Compliance', loanProduct: 'All Products', author: 'RegTech India', rating: 4.9, downloads: 2140, ruleCount: 10, tags: ['RBI', 'PMLA', 'Compliance', 'KYC'], isFeatured: true, isPremium: false, lastUpdated: '2024-06-15' },
]

const CATEGORIES = ['All', 'Vehicle Finance', 'MSME', 'Agri Finance', 'Personal Finance', 'Commercial Finance', 'Compliance']

export function MarketplacePage() {
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState('All')
  const [showPremiumOnly, setShowPremiumOnly] = useState(false)
  const [installed, setInstalled] = useState<Set<string>>(new Set())

  const { data: templates = MOCK_TEMPLATES } = useQuery({
    queryKey: ['marketplace'],
    queryFn: async () => {
      try {
        const res = await apiClient.get<MarketplaceTemplate[]>('/marketplace/templates')
        return res.data
      } catch {
        return MOCK_TEMPLATES
      }
    },
  })

  const filtered = templates.filter(t => {
    const matchSearch = t.name.toLowerCase().includes(search.toLowerCase()) ||
      t.description.toLowerCase().includes(search.toLowerCase()) ||
      t.tags.some(tag => tag.toLowerCase().includes(search.toLowerCase()))
    const matchCategory = category === 'All' || t.category === category
    const matchPremium = !showPremiumOnly || t.isPremium
    return matchSearch && matchCategory && matchPremium
  })

  const featured = filtered.filter(t => t.isFeatured)

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Rule Marketplace</h1>
          <p className="text-gray-500 text-sm mt-1">Discover and import pre-built rule templates</p>
        </div>
        <div className="flex items-center gap-3">
          <span className="text-gray-500 text-sm">{templates.length} templates available</span>
          <button className="btn-secondary">Publish Template</button>
        </div>
      </div>

      {/* Search & Filter */}
      <div className="flex gap-3 items-center">
        <input type="text" placeholder="Search templates, tags, products..."
          value={search} onChange={e => setSearch(e.target.value)} className="input flex-1" />
        <label className="flex items-center gap-2 cursor-pointer whitespace-nowrap">
          <input type="checkbox" checked={showPremiumOnly} onChange={e => setShowPremiumOnly(e.target.checked)}
            className="w-4 h-4 text-blue-600 rounded" />
          <span className="text-sm text-gray-600">Premium Only</span>
        </label>
      </div>

      {/* Category Pills */}
      <div className="flex gap-2 flex-wrap">
        {CATEGORIES.map(cat => (
          <button key={cat} onClick={() => setCategory(cat)}
            className={`px-4 py-1.5 rounded-full text-sm font-medium transition-colors ${category === cat ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}>
            {cat}
          </button>
        ))}
      </div>

      {/* Featured */}
      {featured.length > 0 && category === 'All' && !search && (
        <div>
          <h2 className="text-lg font-semibold text-gray-900 mb-3">⭐ Featured Templates</h2>
          <div className="grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-3 gap-4">
            {featured.map(t => <TemplateCard key={t.id} template={t} installed={installed} onInstall={id => setInstalled(prev => new Set([...prev, id]))} />)}
          </div>
        </div>
      )}

      {/* All Templates */}
      <div>
        {(category !== 'All' || search) && (
          <h2 className="text-lg font-semibold text-gray-900 mb-3">{filtered.length} Result{filtered.length !== 1 ? 's' : ''}</h2>
        )}
        <div className="grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-3 gap-4">
          {filtered.filter(t => !t.isFeatured || category !== 'All' || search).map(t => (
            <TemplateCard key={t.id} template={t} installed={installed} onInstall={id => setInstalled(prev => new Set([...prev, id]))} />
          ))}
        </div>
        {filtered.length === 0 && (
          <div className="text-center py-16 text-gray-400">
            <p className="text-4xl mb-3">🔍</p>
            <p className="font-medium">No templates found</p>
            <p className="text-sm mt-1">Try different keywords or category</p>
          </div>
        )}
      </div>
    </div>
  )
}

function TemplateCard({ template: t, installed, onInstall }: {
  template: MarketplaceTemplate
  installed: Set<string>
  onInstall: (id: string) => void
}) {
  const isInstalled = installed.has(t.id)
  return (
    <div className="card hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between mb-2">
        <div className="flex-1">
          <div className="flex items-center gap-2 mb-1">
            <h3 className="font-semibold text-gray-900 text-sm">{t.name}</h3>
            {t.isPremium && <span className="badge-purple text-xs">PRO</span>}
          </div>
          <p className="text-gray-400 text-xs leading-relaxed">{t.description}</p>
        </div>
      </div>

      <div className="flex gap-2 my-3 flex-wrap">
        <span className="badge-blue">{t.category}</span>
        {t.tags.slice(0, 3).map(tag => (
          <span key={tag} className="px-2 py-0.5 bg-gray-100 text-gray-500 rounded text-xs">{tag}</span>
        ))}
      </div>

      <div className="grid grid-cols-3 gap-2 text-center text-xs my-3 bg-gray-50 rounded-lg p-2">
        <div>
          <p className="font-bold text-gray-900">{t.ruleCount}</p>
          <p className="text-gray-400">Rules</p>
        </div>
        <div>
          <p className="font-bold text-yellow-500">{'★'.repeat(Math.round(t.rating))} {t.rating}</p>
          <p className="text-gray-400">Rating</p>
        </div>
        <div>
          <p className="font-bold text-gray-900">{t.downloads.toLocaleString()}</p>
          <p className="text-gray-400">Downloads</p>
        </div>
      </div>

      <div className="flex items-center justify-between pt-3 border-t border-gray-100">
        <p className="text-xs text-gray-400">by {t.author}</p>
        <button
          onClick={() => onInstall(t.id)}
          disabled={isInstalled}
          className={`text-sm font-medium px-3 py-1.5 rounded-lg transition-colors ${isInstalled ? 'bg-green-100 text-green-700 cursor-default' : 'bg-blue-600 text-white hover:bg-blue-700'}`}>
          {isInstalled ? '✓ Installed' : 'Install'}
        </button>
      </div>
    </div>
  )
}
