// OOXML 文本生成（docx 各部件）+ UTF8 编码
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PaperToWord
{
    static class Ooxml
    {
        const string NsW = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
        const string NsR = "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
        const string NsRel = "xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"";
        const string NsTypes = "xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"";

        const string XmlHead = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

        const string RImage = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
        const string RStyles = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";
        const string RSettings = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings";

        public static byte[] Utf8(string s)
        {
            return new UTF8Encoding(false).GetBytes(s);
        }

        static string Num(long v) { return v.ToString(CultureInfo.InvariantCulture); }

        public static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '&': sb.Append("&amp;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default:
                        if (c < 0x20 && c != '\t') sb.Append(' ');
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        // ============ 文档主体：一页一节，保证每张裁片独占一页 A4 ============
        public static string Document(List<Page> pages)
        {
            StringBuilder sb = new StringBuilder(96 * 1024);
            sb.Append(XmlHead);
            sb.Append("<w:document ").Append(NsW).Append(' ').Append(NsR).Append("><w:body>");

            int docPrId = 1;
            for (int i = 0; i < pages.Count; i++)
            {
                Page pg = pages[i];
                bool last = (i == pages.Count - 1);

                // 图片段落
                sb.Append("<w:p><w:pPr>");
                sb.Append("<w:spacing w:before=\"0\" w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/>");
                sb.Append("<w:jc w:val=\"center\"/>");
                sb.Append("</w:pPr>");
                for (int k = 0; k < pg.RelIds.Count; k++)
                {
                    if (k > 0) sb.Append("<w:r><w:br/></w:r>");
                    sb.Append(PicRun(ref docPrId, pg.RelIds[k], pg.Names[k], pg.EmuW[k], pg.EmuH[k]));
                }
                sb.Append("</w:p>");

                if (!last)
                {
                    // 1 磅高的空段作为该节最后一段，承载分节符，几乎不占版面
                    sb.Append("<w:p><w:pPr>");
                    sb.Append("<w:spacing w:before=\"0\" w:after=\"0\" w:line=\"20\" w:lineRule=\"exact\"/>");
                    sb.Append("<w:rPr><w:sz w:val=\"2\"/><w:szCs w:val=\"2\"/></w:rPr>");
                    sb.Append(SectPr(pg.Landscape));
                    sb.Append("</w:pPr></w:p>");
                }
            }

            // 文档级 sectPr（最后一节）：与最后一页同方向
            sb.Append(SectPr(pages.Count > 0 && pages[pages.Count - 1].Landscape));
            sb.Append("</w:body></w:document>");
            return sb.ToString();
        }

        static string PicRun(ref int id, string relId, string name, long emuW, long emuH)
        {
            string cx = Num(emuW), cy = Num(emuH);
            string sid = Num(id); id++;
            string nm = Esc(name);
            StringBuilder sb = new StringBuilder(800);
            sb.Append("<w:r><w:rPr><w:noProof/></w:rPr><w:drawing>");
            sb.Append("<wp:inline xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" ")
              .Append("distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">");
            sb.Append("<wp:extent cx=\"").Append(cx).Append("\" cy=\"").Append(cy).Append("\"/>");
            sb.Append("<wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>");
            sb.Append("<wp:docPr id=\"").Append(sid).Append("\" name=\"").Append(nm).Append("\"/>");
            sb.Append("<wp:cNvGraphicFramePr>")
              .Append("<a:graphicFrameLocks xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" noChangeAspect=\"1\"/>")
              .Append("</wp:cNvGraphicFramePr>");
            sb.Append("<a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">")
              .Append("<a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">");
            sb.Append("<pic:pic xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">");
            sb.Append("<pic:nvPicPr><pic:cNvPr id=\"").Append(sid).Append("\" name=\"").Append(nm).Append("\"/><pic:cNvPicPr/></pic:nvPicPr>");
            sb.Append("<pic:blipFill><a:blip r:embed=\"").Append(relId).Append("\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>");
            sb.Append("<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"").Append(cx).Append("\" cy=\"").Append(cy).Append("\"/></a:xfrm>");
            sb.Append("<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>");
            sb.Append("</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>");
            return sb.ToString();
        }

        // A4 纵向 210x297mm（landscape=true 时 297x210mm）；四边 5mm；不设文档网格，避免基线撑高
        public static string SectPr()
        {
            return SectPr(false);
        }

        public static string SectPr(bool landscape)
        {
            // w:pgSz 的 w/h 始终是“宽 x 高”（twip）；orient 只是排版提示
            string sz = landscape ? "w:w=\"16838\" w:h=\"11906\" w:orient=\"landscape\""
                                  : "w:w=\"11906\" w:h=\"16838\"";
            return "<w:sectPr>" +
                "<w:pgSz " + sz + "/>" +
                "<w:pgMar w:top=\"283\" w:right=\"283\" w:bottom=\"283\" w:left=\"283\" w:header=\"142\" w:footer=\"142\" w:gutter=\"0\"/>" +
                "<w:cols w:space=\"708\"/>" +
                "</w:sectPr>";
        }

        // ============ 包结构 ============
        public static string ContentTypes()
        {
            StringBuilder sb = new StringBuilder(1024);
            sb.Append(XmlHead);
            sb.Append("<Types ").Append(NsTypes).Append('>');
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            sb.Append("<Default Extension=\"jpg\" ContentType=\"image/jpeg\"/>");
            sb.Append("<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>");
            sb.Append("<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>");
            sb.Append("<Override PartName=\"/word/settings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/>");
            sb.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
            sb.Append("<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");
            sb.Append("</Types>");
            return sb.ToString();
        }

        public static string RootRels()
        {
            return XmlHead +
                "<Relationships " + NsRel + ">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
                "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
                "</Relationships>";
        }

        public static string DocRels(List<Page> pages)
        {
            StringBuilder sb = new StringBuilder(4096);
            sb.Append(XmlHead);
            sb.Append("<Relationships ").Append(NsRel).Append('>');
            for (int i = 0; i < pages.Count; i++)
            {
                Page pg = pages[i];
                for (int k = 0; k < pg.RelIds.Count; k++)
                {
                    string rid = pg.RelIds[k];
                    int idx = int.Parse(rid.Substring(3), CultureInfo.InvariantCulture);
                    sb.Append("<Relationship Id=\"").Append(rid).Append("\" Type=\"").Append(RImage)
                      .Append("\" Target=\"media/image").Append(idx).Append(".jpg\"/>");
                }
            }
            sb.Append("<Relationship Id=\"rStyle\" Type=\"").Append(RStyles).Append("\" Target=\"styles.xml\"/>");
            sb.Append("<Relationship Id=\"rSettings\" Type=\"").Append(RSettings).Append("\" Target=\"settings.xml\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        public static string Styles()
        {
            return XmlHead +
                "<w:styles " + NsW + " " + NsR + ">" +
                "<w:docDefaults><w:rPrDefault><w:rPr>" +
                "<w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:eastAsia=\"宋体\" w:cs=\"Times New Roman\"/>" +
                "<w:sz w:val=\"21\"/><w:szCs w:val=\"21\"/><w:lang w:val=\"en-US\" w:eastAsia=\"zh-CN\" w:bidi=\"ar-SA\"/>" +
                "</w:rPr></w:rPrDefault>" +
                "<w:pPrDefault><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault>" +
                "</w:docDefaults>" +
                "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/></w:style>" +
                "</w:styles>";
        }

        public static string Settings()
        {
            return XmlHead +
                "<w:settings " + NsW + " " + NsR + ">" +
                "<w:zoom w:percent=\"100\"/>" +
                "<w:defaultTabStop w:val=\"420\"/>" +
                "<w:characterSpacingControl w:val=\"compressPunctuation\"/>" +
                "<w:compat>" +
                "<w:compatSetting w:name=\"compatibilityMode\" w:uri=\"http://schemas.microsoft.com/office/word\" w:val=\"15\"/>" +
                "<w:compatSetting w:name=\"overrideTableStyleFontSizeAndJustification\" w:uri=\"http://schemas.microsoft.com/office/word\" w:val=\"1\"/>" +
                "</w:compat>" +
                "</w:settings>";
        }

        public static string Core()
        {
            string now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";
            string who = "PaperToWord";
            return XmlHead +
                "<cp:coreProperties " +
                "xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
                "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" " +
                "xmlns:dcterms=\"http://purl.org/dc/terms/\" " +
                "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                "<dc:title>" + Esc("试卷（裁页版）") + "</dc:title>" +
                "<dc:creator>" + Esc(who) + "</dc:creator>" +
                "<cp:lastModifiedBy>" + Esc(who) + "</cp:lastModifiedBy>" +
                "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:created>" +
                "<dcterms:modified xsi:type=\"dcterms:W3CDTF\">" + now + "</dcterms:modified>" +
                "</cp:coreProperties>";
        }

        public static string App()
        {
            return XmlHead +
                "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" " +
                "xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">" +
                "<Application>PaperToWord</Application>" +
                "<DocSecurity>0</DocSecurity><ScaleCrop>false</ScaleCrop>" +
                "<AppVersion>1.2000</AppVersion>" +
                "</Properties>";
        }
    }
}
