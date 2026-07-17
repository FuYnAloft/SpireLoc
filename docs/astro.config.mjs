// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

const site = 'https://fuynaloft.github.io';
const base = '/SpireLoc';
const documentationArchiveUrl = `${site}${base}/downloads/SpireLoc-docs.zip`;

// https://astro.build/config
export default defineConfig({
	site,
	base,
	integrations: [
		starlight({
			title: 'SpireLoc',
			description: 'Localization pipelines for Slay the Spire 2 mods.',
			customCss: ['./src/styles/custom.css'],
			defaultLocale: 'en',
			locales: {
				en: { label: 'English', lang: 'en' },
				'zh-cn': { label: '简体中文', lang: 'zh-CN' },
			},
			social: [
				{ icon: 'github', label: 'GitHub', href: 'https://github.com/FuYnAloft/SpireLoc' },
			],
			editLink: {
				baseUrl: 'https://github.com/FuYnAloft/SpireLoc/edit/main/docs/',
			},
			sidebar: [
				{
					label: 'Start Here',
					translations: { 'zh-CN': '从这里开始' },
					items: [
						{ slug: 'start/why-spireloc' },
						{ slug: 'start/setup' },
						{ slug: 'start/ritsulib' },
						{ slug: 'start/baselib' },
					],
				},
				{
					label: 'Guides',
					translations: { 'zh-CN': '指南' },
					items: [
						{ slug: 'guides/pipeline' },
						{
							label: 'Actions',
							translations: { 'zh-CN': 'Action' },
							items: [
								{ slug: 'guides/actions/using-actions' },
								{ slug: 'guides/actions/writing-actions' },
							],
						},
					],
				},
				{
					label: 'Reference',
					translations: { 'zh-CN': '参考' },
					items: [
						{
							label: 'Comparison with Game Syntax',
							translations: { 'zh-CN': '与原版的写法对比' },
							items: [
								{ slug: 'reference/syntax/ritsulib' },
								{ slug: 'reference/syntax/baselib' },
							],
						},
						{
							label: 'Operations',
							translations: { 'zh-CN': '所有 Operations' },
							items: [
								{ slug: 'reference/operations/input' },
								{ slug: 'reference/operations/output' },
								{ slug: 'reference/operations/model-id' },
								{ slug: 'reference/operations/compat' },
								{ slug: 'reference/operations/reshape' },
								{ slug: 'reference/operations/partition' },
								{ slug: 'reference/operations/merge' },
								{ slug: 'reference/operations/copy' },
							],
						},
						{ slug: 'reference/builtin-actions' },
						{ slug: 'reference/action-syntax' },
					],
				},
				{
					label: 'Download all documentation (zip)',
					translations: { 'zh-CN': '下载全部文档（zip）' },
					link: documentationArchiveUrl,
					attrs: {
						'data-download-docs': true,
						download: 'SpireLoc-docs.zip',
					},
				},
			],
		}),
	],
});
