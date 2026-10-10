function copy_trackback(trb) {

    var agent = navigator.userAgent.toLowerCase();
    if ( (navigator.appName == 'Netscape' && agent.indexOf('trident') != -1) || (agent.indexOf("msie") != -1)) {
         // ie�� ���
         if(confirm("Ŭ�����忡 �����Ͻðڽ��ϱ�?"))
             window.clipboardData.setData("Text", trb);
    }else{
         // ie�� �ƴ� ���
         temp = prompt("Ctrl+C�� ���� Ŭ������� �����ϼ���", trb);
    }
}

// SNS ���
var sns_name, sns_url, sns_width, sns_height;

function goSns(sns_name, sns_url, sns_width, sns_height) {
  window.open(
    sns_url,
    sns_name,
    "width=" + sns_width + ", height=" + sns_height + ", resizable=yes, scrollbars=yes"
  );
}

// X (Twitter) ����
function go_twitter() {
  sns_name   = "twitter";
  sns_width  = 800;
  sns_height = 425;

  var text = document.title; // ������ �ؽ�Ʈ
  var url  = document.URL;   // ������ URL

  // �ֽ� ���� URL
  sns_url = "https://twitter.com/intent/tweet?text=" 
          + encodeURIComponent(text) 
          + "&url=" + encodeURIComponent(url);

  goSns(sns_name, sns_url, sns_width, sns_height);
}


// ���̽���
function go_facebook() {
  sns_name = "facebook";
  sns_url = "http://www.facebook.com/sharer.php?t=" + encodeURIComponent(document.title) + "&u=" + encodeURIComponent(document.URL);
  sns_width = 800;
  sns_height = 400;
  goSns(sns_name, sns_url, sns_width, sns_height);
}

// ���̹� ���α�
function go_naver_blog() {
  sns_name = "naver_blog";
  sns_url = "http://share.naver.com/web/shareView.nhn?title=" + encodeURIComponent(document.title) + "&url=" + encodeURIComponent(document.URL);
  sns_width = 430;
  sns_height = 500;
  goSns(sns_name, sns_url, sns_width, sns_height);
}

// Ʈ����
function go_twitter_info(title, url) {
  sns_name = "twitter";
  sns_url = "http://twitter.com/home?status=" + encodeURIComponent(title + ' ') + escape(url);
  sns_width = 800;
  sns_height = 425;
  goSns(sns_name, sns_url, sns_width, sns_height);
}

// ���̽���
function go_facebook_info(title, url) {
  sns_name = "facebook";
  sns_url = "http://www.facebook.com/sharer.php?t=" + encodeURIComponent(title) + "&u=" + encodeURIComponent(url);
  sns_width = 800;
  sns_height = 400;
  goSns(sns_name, sns_url, sns_width, sns_height);
}

// ���̹� ���α�
function go_naver_blog_info(title, url) {
  sns_name = "naver_blog";
  sns_url = "http://share.naver.com/web/shareView.nhn?title=" + encodeURIComponent(title) + "&url=" + encodeURIComponent(url);
  sns_width = 430;
  sns_height = 500;
  goSns(sns_name, sns_url, sns_width, sns_height);
}

// ���� �޼���
function statusMsg(status) {

	var result_msg;

	switch(status) {
    	case "2" :
    		result_msg = "�α��� �� �̿��� �ּ���.";
    		break;
    	case "3" :
    		result_msg = "������ ���� ������ ���� �������ּ���.";
    		break;
    	case "4" :
    		result_msg = "��ǥĳ���͸� ���� ������ �ּ���.";
    		break;
        case "8" :
    		result_msg = "����Ⱓ ���� Ȩ������ Ŀ�´�Ƽ Ȱ���� �Ͻ� �� �����ϴ�.";
    		break;
      case "9" :
      	result_msg = "�� 3�� �������� ���� �� �̿����ּ���.";
      	break;
    	default :
    		result = "";
    		break;
    }

    return result_msg;
}

// ���� üũ
function checkStatus() {

	var result = false;

	 $.ajax({
        url: "/page/common/status/check_status.asp",
		async: false,
        type: "post",
        dataType: "text",
        data: {},
        timeout: 30000,
        success: function (data) {

           switch(data) {
	        	case "1" :
	        		result = true;
	        		break;
	        	default :
					location.href='https://nxlogin.nexon.com/auth/login?redirect=' + encodeURIComponent(location.href);
	        		//alert(statusMsg(data));
	        		result = false;
	        		break;
	        }
        },
        error: function (xhr, textStatus, errorThrown) {

        }
    });

    return result;
}

// getCookie :: ��Ű�� �о�´�.
function getCookie(name)
{
	var nameOfCookie = name + "=";
	var x = 0;
	while (x <= document.cookie.length)
	{
		var y = (x+nameOfCookie.length);
		if (document.cookie.substring( x, y ) == nameOfCookie)
		{
			if ((endOfCookie=document.cookie.indexOf(";",y)) == -1)
			{
				endOfCookie = document.cookie.length;
			}
			return unescape(document.cookie.substring(y,endOfCookie));
		}

		x = document.cookie.indexOf(" ",x) + 1;
		if (x == 0)
		{
			break;
		}
	}
	return "";
}


function setCookieForToday(name, value) {
  var dtToday = new Date();
  dtToday.setDate(dtToday.getDate() + 2);
  document.cookie = name + "=" + escape(value) + "; path=/; expires=" + dtToday.toGMTString() + ";";
}

/*	setCookie */
function setCookie(name,value,expiredays) {
	var todayDate = new Date();
	todayDate.setDate(todayDate.getDate() + expiredays);
	document.cookie = name + "=" + escape(value) + "; path=/; expires=" + todayDate.toGMTString() + ";";
}

/*	setCookie */
function setCookiePage(name,value,expiredays) {
	var todayDate = new Date();
	todayDate.setDate(todayDate.getDate() + expiredays);
	document.cookie = name + "=" + escape(value) + "; path=/; expires=" + todayDate.toGMTString() + ";";
}

function setCookieDomain(name,value,expiredays) {
	var todayDate = new Date();
	todayDate.setDate(todayDate.getDate() + expiredays);
	document.cookie = name + "=" + escape(value) + "; path=/; expires=" + todayDate.toGMTString() + "; Domain=mabinogi.nexon.com"; 

}

function deleteCookie(cookieName) {
  var expireDate = new Date();

  //���� ��¥�� ��Ű �Ҹ� ��¥�� �����Ѵ�.
  expireDate.setDate( expireDate.getDate() - 1 );
  document.cookie = cookieName + "= " + "; expires=" + expireDate.toGMTString() + "; path=/";
}

// �ؽ�Ʈ ����� ���� üũ
function textAreaLenCheck(this_id, len, txt) {

  var this_val = $("#"+this_id).val();
  var this_len = $("#"+this_id).val().length;

  if(this_len > len) {
    $("#"+this_id).val(this_val.substring(0, len));
    this_len = len;
  }

  var current_len = txt.replace("?", this_len);

  $("#"+this_id+"_"+"lencheck").html(current_len);
}

// ��������
function helpPop() {
  if(location.host == "mabinogi.nexon.game.naver.com") {
    if(!checkStatus()) return;
    window.open('http://support.nexon.game.naver.com/HelpBoard/Nexon?gamecode=21','helpPop','width=1024,height=768,scrollbars=yes');
  } else {
    window.open('http://cs.nexon.com/HelpBoard/Nexon?gamecode=21','helpPop','width=1024,height=768,scrollbars=yes');
  }
}

// �������� ī�װ���
function helpPopCategory(category) {
  if(location.host == "mabinogi.nexon.game.naver.com") {
    if(!checkStatus()) return;
    window.open('http://support.nexon.game.naver.com/HelpBoard/Nexon?gamecode=21&category='+category,'helpPop','width=1024,height=768,scrollbars=yes');
  } else {
    window.open('http://cs.nexon.com/HelpBoard/Nexon?gamecode=21&category='+category,'helpPop','width=1024,height=768,scrollbars=yes');
  }
}

// JS_trim :: trim �Լ�
function JS_trim(data)
{
	for(var i = 0; i < data.length; i++)
	{
		var digit = data.charAt(i)
		if(digit == " ")
			continue;
		else
			return 1;
	}
	return -1;
}

function openGuild(guild_id) {
  location.href = "/page/community/guild_bbslist.asp?guildid="+guild_id;
}

// LPAD
function lpad(s, padLength, padString) {
  while(s.length < padLength)
    s = padString + s;
  return s;
}

// RPAD
function rpad(s, padLength, padString) {
  while(s.length < padLength)
    s += padString;
  return s;
}

// ���� ���̾�
function sendLayer(){
		$(".pop_box.ty01").layerCenter();
		$("#memo_area").show();
}

// �������̾� �ݱ�
function sendLayerClose() {
  $("#memo_area").hide();
}

// ���� ���̾�
function sendLayerUserInfo(user_server, user_character){
    if(!checkStatus()) return;

		$(".pop_box.ty01").layerCenter();
		$("#memo_area").show();

    $("select[name=ToSvrName]").val(user_server).attr("selected", "selected");
    $("input[name=ToCharName]").val(user_character);
}

// ���̾� ����
(function($){
	$.fn.layerCenter = function(id){
		$(this).css("position","absolute");
		var win = $(window);
		var ts = $(this);
		var x = win.width();
		var y = win.height();
		if(!id){
			var left = win.scrollLeft() + x/2 - ts.width()/2;
			var top = win.scrollTop() + y/2 - ts.height()/2;
		}else{
			var po = $("#"+id).position();
			var top = parseInt((($("#"+id).height() / 2) + po.top) - (ts.height() / 2));
			var left = parseInt((($("#"+id).width() / 2) + po.left) - (ts.width() / 2));
			top = top<po.top ? po.top : top;
		}
		$(this).css("left",left);
		$(this).css("top",top - 800);
	}
})(jQuery);

//ReadMemo :: �޸� �б�
function ReadMemo(MemoNumber, ReadFlag, ReadType, ReadType2, flag){
	location.href='memo_view.asp?MemoNumber='+MemoNumber+'&ReadType='+ReadType+'&ReadType2='+ReadType2+'&ReadFlag='+ReadFlag+'&flag='+ flag
}

// ���� ��������Ʈ �߰�
function balcklistadd()
{
	var check_nums = document.list.elements.length;
	for(var i = 0; i < check_nums; i++) {
		var checkbox_obj = eval("document.list.elements[" + i + "]");
		if(checkbox_obj.checked == true) break;
	}
	if(i == check_nums) {
		alert("���õ� �Խù��� �����ϴ�.");
		return;
	}
	var result = confirm("����ڸ� ��������Ʈ�� �߰� �Ͻðڽ��ϱ�?");
	if ( result == true ) {
		document.list.Flag.value = 11;
		document.list.action = "memo_receive.asp";
		document.list.submit();
	}
}

// ���� ����
function delitem()
{
	var check_nums = document.list.elements.length;
	for(var i = 0; i < check_nums; i++) {
		var checkbox_obj = eval("document.list.elements[" + i + "]");
		if(checkbox_obj.checked == true) break;
	}
	if(i == check_nums) {
		alert("���õ� �Խù��� �����ϴ�.");
		return;
	}
	var result = confirm("�����Ͻðڽ��ϱ�?");
	if ( result == true ) {
		document.list.action = "memo_receive.asp";
		document.list.submit();
	}
}

function goNxMemberJoin() {
    NgbMember.GoRegisterPage(138);
}
function goNxFindEmailID() {
	window.open('https://member.nexon.com/find/findid.aspx', 'FindIDPwNx', 'width=500, height=600, resizable=no, menubar=no, scrollbars=no, status=no, titlebar=no, location=no, directories=no');
	window.focus();
}
function goNxFindEmailPassword() {
	window.open("https://member.nexon.com/find/findpwd.aspx", 'FindIDPwNx', 'width=500, height=600, resizable=no, menubar=no, scrollbars=no, status=no, titlebar=no, location=no, directories=no');
	window.focus();
}

function transferToEmail() {
    window.open('https://trans.nexon.com/trans/transemailid.aspx', 'transferEmailAccountWindow');
}

function goNxMyinfo() {
    window.open('https://user.nexon.com/mypage/page/nx.aspx?url=myinfomanage/changemyinfo', 'myinfoWindow');
}

function unifyGameID() {
	//alert("��� �����ϱ� ���� �ڵ� �α׾ƿ� �˴ϴ�.");
	window.open('https://mabinogi.nexon.com/page/member/intergration/integrationAccount_Step1.asp', 'unifyAccountWindow','width=500, height=463');
	//location.href = "/page/member/logout.asp";
}

// ���� ���� ���̾�
function CreatAccount() {
  location.href = "https://mabinogi.nexon.com/page/member/create_account_prss.asp";
}

function getAllParams() {
	const params = new URLSearchParams(window.location.search);
	const queryString = params.toString();

	// �ٷ� �����̷�Ʈ
	return queryString;
}

function CreatAccount(p) {
	location.href = "/page/member/create_account_prss.asp?"+ p;
}

// ���� ���� ���� ���̾�
function selectMultiMabinogiId(val, num) {
  $("a[id^=li_multi_]").removeClass("on");
  $("#li_multi_"+num).addClass("on");
  $("ul[id^=ul_multi_]").hide();
  $("#ul_multi_"+num).show();
  $("#selectedID").val(val);
}

function SelectMultiAccount() {
  $("#multiForm").submit();
}


function openUserInfoLayer(infolayer_id, infolayer_server, infolayer_character) {
  $.post("/page/common/page/community/userinfolayerData.asp", {infolayer_id: infolayer_id, infolayer_server:infolayer_server, infolayer_character:escape(infolayer_character)}, function(data) {
    $("#user_info_layer").html(data);
    $("#user_info_layer").show();
    $(".pop_user_info").layerCenter();
    // �����ܿ� Ŭ���� �߰�
    $("#user_info_layer .inner_box img").addClass("icon");
  });
}

function openUserInfoLayerMemo(infolayer_server, infolayer_character) {
  sendLayerUserInfo(infolayer_server, infolayer_character);
  popCloseLayer('user_info_layer');

}

function imgViewer(url, width, height) {

  var xPos = (document.body.clientWidth / 2) - (width / 2);
      xPos += window.screenLeft;  //��� ������϶�....
  var yPos = (screen.availHeight / 2) - (height / 2);

  window.open("/page/common/bbs/OpenImg.asp?file="+url+"&w="+width+"&h="+height, 'img_view_pop', "width="+width+",height="+height+",left="+xPos+",top="+yPos);
}

// ���ȼ���
function goSecurityCenter() {
  if(location.host == "mabinogi.nexon.game.naver.com") {
    window.open('https://nid.naver.com/user2/help/myInfo.nhn?m=viewSecurity&lang=ko_KR', 'security_pop');
  } else {
    window.open('http://security.nexon.com/main/index.aspx', 'security_pop');
  }

}

function goYoutubePage() {
  window.open('https://www.youtube.com/user/Mabifantasy', 'youtube_pop');
}

function goTwitterPage() {
  window.open('https://twitter.com/Nexon_Mabinogi', 'twitter_pop');
}

function goFacebookPage() {
  window.open('http://www.facebook.com/nexon.mabinogi', 'facebook_pop');
}

function OpenGameUseGuide() {
  window.open('/page/main/pop_game_use.asp','game_use','width=500,height=504,scrollbars=no');
}

// ��� Ŭ��
function goList(url) {
  var refer = document.referrer;

  var path_name_list = $(location).attr("pathname");
  var path_arry_list = path_name_list.split("/");
  var folder_name_list = path_arry_list[2];  // ex) news
  var file_name_list = path_arry_list[3]; // ex) notice_list.asp

  var file_arry_list = file_name_list.split('_');
  var menu_name_list = file_arry_list[0]; // ex) notice

  if(refer.indexOf(menu_name_list) <= 0) {
    location.href = url;
  } else {
    if($("#comment_refer").length > 0) {
        if($("#comment_refer").val().indexOf("_list.asp") > 0) {
            location.href = $("#comment_refer").val();
        } else {
            location.href = url;
        }
    } else {
        history.back();
    }
  }
}

// Ȩ������ �̵� ��Ű ����
function setHomeCookie(day) {
  if(day > 0) {
    var dtToday = new Date();
    dtToday.setDate(dtToday.getDate() + day);
    document.cookie = "introMovie=1; path=/; expires=" + dtToday.toGMTString() + ";";
  } else {
    document.cookie = "introMovie=1; path=/;";
  }
}

// Ȩ������ �ٷΰ���
function go_home() {
  setHomeCookie(0);
  location.href = "/page/main/index.asp";
}

// �����Ϸ� �����ʱ�
function go_home_today() {
  setHomeCookie(1);
  location.href = "/page/main/index.asp";
}

// ȸ������
function member_info() {
  if(location.host == "mabinogi.nexon.game.naver.com") {
    gdp.jslib.goLink('MyInfo');
  } else {

      $.ajax({
         url: "/page/mymenu/check_member_info.asp",
         async: false,
         type: "post",
         dataType: "text",
         data: {},
         timeout: 30000,
         success: function (data) {
             if(data == "0") {
                 location.href = "/page/mymenu/password.asp";
             } else {
                 window.open('https://member.nexon.com/manage/changemyinfo.aspx', '_blank');
             }
         },
         error: function (xhr, textStatus, errorThrown) {

         }
     });
  }
}

// ������ ������ �̵� (��Ʈ�� ����)
function go_mabi_page(url) {
  document.cookie = "introMovie=1; path=/;";
  location.href = url;
}

function go_mabi_page_blank(url) {
  document.cookie = "introMovie=1; path=/;";
  window.open(url, "_blank");
}

// ���̾� ���� (Ŭ������, ž�ȼ��߰�, ����Ʈ �ȼ��߰�)
function open_layer_area(val, add_top, add_left) {
  $(".bg_dimmed").show();
  $("." + val).layerCenterShow(add_top, add_left);
}

// ���̾� �ݱ�
function close_layer_area(val) {
  $(".bg_dimmed").hide();
  $("." + val).hide();
}

// ��÷�� Ȯ��
function winner(num) {

    if(!checkStatus()) return;

	 $.ajax({
        url: "/page/common/status/winner/winner_status.asp",
        async: false,
        type: "post",
        dataType: "text",
        data: {num:num},
        timeout: 30000,
        success: function (data) {
            // �������� �����ؼ� �����̸� ���� �޽����� �����ش�.
            if($.isNumeric(data)) {
                alert(statusMsg(data));
            } else {
                alert(data);
            }

        },
        error: function (xhr, textStatus, errorThrown) {

        }
    });
}

// ���̾� ����
(function($){
  $.fn.layerCenterShow = function(add_top, add_left) {

    $(this).css("position","absolute");
    var win = $(window);
    var ts = $(this);
    var x = win.width();
    var y = win.height();

    var top = win.scrollTop() + y/2 - ts.height()/2;
    var left = win.scrollLeft() + x/2 - ts.width()/2;

    $(this).css("top", top + add_top);
    $(this).css("left", left + add_left);

    $(this).show();
  }
})(jQuery);


// ���� �ѵ� ��ȸ
function couponlimit() {

    if(!checkStatus()) return;

	 $.ajax({
        url: "/page/common/limitapi.asp",
        async: false,
        type: "post",
        dataType: "text",
        timeout: 30000,
        success: function (data) {            
			alert(data);
        },
        error: function (xhr, textStatus, errorThrown) {

        }
    });
}

// �Խ��� new �̸�Ƽ�� �߰� �Լ�
function fetchNoticeData() {
	
	$.ajax({
		url: "/page/common/noticeapi.asp?t=1",
		method: "GET",
		dataType: "text",
		success: function(data) {
			const newDotIcon = '<div class="new_disc"></div>';

			console.log("���� ������:", data);

			const parts = data.split("/");
			const isAPiNew = parts[0];
			const boardIds = parts.slice(1);
			const isCampaignNew = parts[9];

			//console.log("isNew:", isCampaignNew);
			//console.log("�Խ��� IDs:", boardIds);

			const boardMap = [
				{ label: "��������", selectors: ["#sub_menu li a", "#menu_news li a", ".board_tab_ty01 ul li a"] },
				{ label: "�����ڳ�Ʈ", selectors: ["#sub_menu li a", "#menu_news li a"] },
				{ label: "���� ���̵�", selectors: ["#menu_intro li a"] },
				{ label: "������", selectors: [".board_tab_ty01 ul li a"], sharedLabel: "��Ƽ�̵��" },
				{ label: "����", selectors: [".board_tab_ty01 ul li a"], sharedLabel: "��Ƽ�̵��" },
				{ label: "��������", selectors: [".board_tab_ty01 ul li a"], sharedLabel: "������" },
				{ label: "�����ڷ�", selectors: [".board_tab_ty01 ul li a"], sharedLabel: "������" },
				{ label: "�Ϸ���Ʈ", selectors: [".board_tab_ty01 ul li a"], sharedLabel: "������" },
			];

			const appliedSelectors = new Set();

			// �ؽ� ���� ������ �߰�
			if (isAPiNew === "1") {
					// �ؽ� ����
					const selectorNexon = ".board_tab_ty01 ul li a";
					const labelNexon = "�ؽ� ����";
					const keyNexon = `${selectorNexon}|${labelNexon}`;
					if (!appliedSelectors.has(keyNexon)) {
							addNewIcon(selectorNexon, labelNexon, newDotIcon);
							appliedSelectors.add(keyNexon);
					}

					// ��������
					const boardNotice = { label: "��������", selectors: ["#sub_menu li a", "#menu_news li a"] };
					boardNotice.selectors.forEach(selector => {
							const keyNotice = `${selector}|${boardNotice.label}`;
							if (!appliedSelectors.has(keyNotice)) {
									addNewIcon(selector, boardNotice.label, newDotIcon);
									appliedSelectors.add(keyNotice);
							}
					});
			}

			// ��ȸ���� ������ �߰� 
			if (isCampaignNew === "1") {
				const selector = "#menu_intro li a";
				const label = "��ȸ ����";
				const key = `${selector}|${label}`;
				if (!appliedSelectors.has(key)) {
					addNewIcon(selector, label, newDotIcon);
					appliedSelectors.add(key);
				}
			}

			// �Խ��� �׸� ������ ó��
			boardIds.forEach((id, index) => {
				if (id !== "") {
					const item = boardMap[index];
					if (item) {
						// ���� label (��Ƽ�̵��, ������ ��)
						if (item.sharedLabel) {
							["#sub_menu li a", "#menu_pds li a"].forEach(sharedSelector => {
								const key = `${sharedSelector}|${item.sharedLabel}`;
								if (!appliedSelectors.has(key)) {
									addNewIcon(sharedSelector, item.sharedLabel, newDotIcon);
									appliedSelectors.add(key);
								}
							});
						}

						// ���� label
						item.selectors.forEach(selector => {
							const key = `${selector}|${item.label}`;
							if (!appliedSelectors.has(key)) {
								addNewIcon(selector, item.label, newDotIcon);
								appliedSelectors.add(key);
							}
						});
					}
				}
			});
			
		},
		error: function(xhr, status, error) {
			console.error("�����͸� �������� �� ���� �߻�: ", error);
		}
	});

	function addNewIcon(selector, label, iconHTML) {
		const menuItem = $(selector).filter(function() {
			return $(this).text().trim() === label;
		});
		if (menuItem.length && !menuItem.hasClass("new-attached")) {
			menuItem.append(iconHTML).addClass("new-attached");
		}
	}
}
